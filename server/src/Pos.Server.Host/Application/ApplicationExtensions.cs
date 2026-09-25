namespace Pos.Server.Host.Application;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;

using MiniDataProfiler;
using MiniDataProfiler.Listener.Logging;
using MiniDataProfiler.Listener.OpenTelemetry;

using MudBlazor;
using MudBlazor.Services;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Pos.Server.Accessors;
using Pos.Server.Host.Application.State;
using Pos.Server.Host.Application.Telemetry;
using Pos.Server.Host.Components;
using Pos.Server.Host.Endpoints;
using Pos.Server.Host.Infrastructure.ExceptionHandling;
using Pos.Server.Host.Infrastructure.Logging;
using Pos.Server.Host.Reports;
using Pos.Server.Infrastructure.Json;
using Pos.Server.Infrastructure.Security;
using Pos.Server.Services;

using Serilog;

using Smart.Data;

public static class ApplicationExtensions
{
    private const string SchemaPath = "Assets/Data/Schema.sql";
    private const string InitialDataPath = "Assets/Data/InitialData.sql";
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    private const string ApiPathPrefix = "/api";

    //--------------------------------------------------------------------------------
    // System
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureSystem(this WebApplicationBuilder builder)
    {
        // Path
        builder.Configuration.SetBasePath(AppContext.BaseDirectory);

        // Encoding
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Host
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureHost(this WebApplicationBuilder builder)
    {
        // Service
        builder.Services
            .AddWindowsService()
            .AddSystemd();

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Logging
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureLogging(this IHostApplicationBuilder builder)
    {
        var setting = builder.Configuration.GetSection("Log").Get<LogSetting>()!;
        var useOtlpExporter = builder.Configuration.IsOtelExporterEnabled();

        // Application log
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(
            (provider, options) =>
            {
                var accessor = provider.GetRequiredService<IHttpContextAccessor>();
                options.ReadFrom.Configuration(builder.Configuration);
                options.Enrich.With(new CallbackEnricher("RemoteIpAddress", () => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString()));
            },
            writeToProviders: useOtlpExporter);

        // HTTP log
        builder.Services.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.RequestMethod |
                                    HttpLoggingFields.RequestPath |
                                    HttpLoggingFields.ResponseStatusCode |
                                    HttpLoggingFields.Duration;
            if (setting.HttpDump)
            {
                options.LoggingFields |= HttpLoggingFields.RequestBody | HttpLoggingFields.ResponseBody;
                options.CombineLogs = true;
                options.RequestBodyLogLimit = setting.HttpDumpLimit;
                options.ResponseBodyLogLimit = setting.HttpDumpLimit;
                options.MediaTypeOptions.Clear();
                options.MediaTypeOptions.AddText("application/json");
                options.MediaTypeOptions.AddText("application/*+json");
            }
        });

        // Access log (W3C)
        if (setting.W3CLog.Enable)
        {
            builder.Services.AddW3CLogging(options =>
            {
                options.LogDirectory = setting.W3CLog.Directory;
                options.FileName = setting.W3CLog.FileName;
                options.RetainedFileCountLimit = setting.W3CLog.RetainedFileCount;
            });
        }

        return builder;
    }

    public static WebApplication UseW3CLog(this WebApplication app)
    {
        var setting = app.Services.GetRequiredService<LogSetting>();
        if (setting.W3CLog.Enable)
        {
            app.UseW3CLogging();
        }

        return app;
    }

    public static WebApplication UseHttpLog(this WebApplication app)
    {
        var setting = app.Services.GetRequiredService<LogSetting>();
        if (setting.HttpLog)
        {
            app.UseWhen(
                static context => context.Request.Path.StartsWithSegments(ApiPathPrefix, StringComparison.OrdinalIgnoreCase),
                static b => b.UseHttpLogging());
        }

        return app;
    }

    //--------------------------------------------------------------------------------
    // Http
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureHttp(this IHostApplicationBuilder builder)
    {
        // ログの文脈 (接続元アドレス) の取得元
        builder.Services.AddHttpContextAccessor();

        // XForward
        builder.Services.Configure<ForwardedHeadersOptions>(static options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Do not restrict to local network/proxy
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return builder;
    }

    //--------------------------------------------------------------------------------
    // API
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureApi(this IHostApplicationBuilder builder)
    {
        // JSON
        builder.Services.ConfigureHttpJsonOptions(static options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = NamingPolicy.JsonPropertyNaming;
            options.SerializerOptions.DictionaryKeyPolicy = NamingPolicy.JsonDictionaryKeyNaming;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
            options.SerializerOptions.Converters.Add(new JsonDateTimeConverter());
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        // Validation
        builder.Services.AddValidation();

        // Error handler
        builder.Services.AddProblemDetails(static options =>
        {
            options.CustomizeProblemDetails = static context =>
            {
                context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

                // 入力検証 (AddValidation) の 400 にも errorCode を付ける
                if (context.ProblemDetails.Status == StatusCodes.Status400BadRequest)
                {
                    context.ProblemDetails.Extensions.TryAdd("errorCode", ErrorCode.ValidationError.ToCode());
                }
            };
        });
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        // 要求の読み取りの失敗 (壊れた JSON・型の合わない引数) は既定では開発環境でだけ例外になり、本番は本文のない 400 になる。
        // どの環境でも例外にし、GlobalExceptionHandler が Problem Details (400 + VALIDATION_ERROR) で返す
        builder.Services.Configure<RouteHandlerOptions>(static options => options.ThrowOnBadRequest = true);

        return builder;
    }

    public static WebApplication UseErrorHandler(this WebApplication app)
    {
        // Page: not found (re-execution does not work inside a UseWhen branch)
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

        // API: ProblemDetails, status code as is
        app.UseWhen(
            static context => context.Request.Path.StartsWithSegments(ApiPathPrefix, StringComparison.OrdinalIgnoreCase),
            static b =>
            {
                b.UseExceptionHandler();
                b.Use(static (context, next) =>
                {
                    context.Features.Get<IStatusCodePagesFeature>()?.Enabled = false;

                    return next(context);
                });
            });

        // Page: error page (UseWhen 内の再実行は暗黙のルーティングに乗らないため、Program.cs で UseErrorHandler の直後に UseRouting を明示する)
        app.UseWhen(
            static context => !context.Request.Path.StartsWithSegments(ApiPathPrefix, StringComparison.OrdinalIgnoreCase),
            static b => b.UseExceptionHandler("/error", createScopeForErrors: true));

        return app;
    }

    //--------------------------------------------------------------------------------
    // Authentication
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureAuthentication(this IHostApplicationBuilder builder)
    {
        var setting = builder.Configuration.GetSection("Auth").Get<AuthSetting>()!;

        // 管理画面はログイン (Cookie)、端末はペアリングで受け取ったトークン (Bearer)
        builder.Services
            .AddAuthentication(AuthSchemes.Cookie)
            .AddCookie(AuthSchemes.Cookie, options =>
            {
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/access-denied";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(setting.ExpireMinutes);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                // 店内の LAN で HTTP のまま動かすこともあるので、HTTPS のときだけ Secure にする
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Events = new CookieAuthenticationEvents
                {
                    // API はログイン画面へ転送せず、状態コードを返す
                    OnRedirectToLogin = static context => RedirectOrStatusAsync(context, StatusCodes.Status401Unauthorized),
                    OnRedirectToAccessDenied = static context => RedirectOrStatusAsync(context, StatusCodes.Status403Forbidden),
                    OnValidatePrincipal = ValidateAccountAsync
                };
            })
            .AddScheme<AuthenticationSchemeOptions, TerminalAuthenticationHandler>(AuthSchemes.Terminal, null);

        // 認証を無効にしたときも、スキームは残して (ログインやトークンがあれば) クレームを付ける
        AuthorizationPolicy BuildPolicy(Action<AuthorizationPolicyBuilder> require, params string[] schemes)
        {
            var policy = new AuthorizationPolicyBuilder(schemes);
            if (setting.Enabled)
            {
                require(policy);
            }
            else
            {
                policy.RequireAssertion(static _ => true);
            }

            return policy.Build();
        }

        // グループ (Api) とエンドポイント (Admin など) のポリシーを重ねるとスキームは合算されるので、
        // 管理画面と端末はスキームではなく要件 (アカウントの役割・端末のクレーム) で区別する
        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = BuildPolicy(static x => x.RequireRole(nameof(AccountRole.Administrator), nameof(AccountRole.Operator)), AuthSchemes.Cookie);
            options.AddPolicy(Policies.Admin, options.DefaultPolicy);
            options.AddPolicy(Policies.Administrator, BuildPolicy(static x => x.RequireRole(nameof(AccountRole.Administrator)), AuthSchemes.Cookie));
            options.AddPolicy(Policies.Api, BuildPolicy(static x => x.RequireAuthenticatedUser(), AuthSchemes.Cookie, AuthSchemes.Terminal));
            options.AddPolicy(Policies.Terminal, BuildPolicy(static x => x.RequireAssertion(static context => AuthClaims.TerminalOf(context.User) is not null), AuthSchemes.Terminal));
        });

        // ログインとペアリングの総当たりを防ぐ (接続元ごと)
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimits.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                _ => new FixedWindowRateLimiterOptions { PermitLimit = setting.AttemptsPerMinute, Window = TimeSpan.FromMinutes(1) }));
            // ログイン画面からの送信は画面に戻して知らせる
            options.OnRejected = static (context, _) =>
            {
                if (!context.HttpContext.Request.Path.StartsWithSegments(ApiPathPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    context.HttpContext.Response.Redirect("/login?error=limit");
                }

                return ValueTask.CompletedTask;
            };
        });

        return builder;
    }

    private static Task RedirectOrStatusAsync(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        if (context.Request.Path.StartsWithSegments(ApiPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = statusCode;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }

        return Task.CompletedTask;
    }

    // アカウントの削除・無効化・変更 (版が変わる) でログイン中のセッションを無効にする
    private static async Task ValidateAccountAsync(CookieValidatePrincipalContext context)
    {
        var account = context.Principal is null ? null : AuthClaims.AccountOf(context.Principal);
        var service = context.HttpContext.RequestServices.GetRequiredService<AccountService>();
        if ((account is null) || !await service.IsSessionValidAsync(account.Id, account.Version, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(AuthSchemes.Cookie);
        }
    }

    //--------------------------------------------------------------------------------
    // Compress
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureCompression(this IHostApplicationBuilder builder)
    {
        builder.Services.AddResponseCompression(static options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        return builder;
    }

    public static WebApplication UseCompression(this WebApplication app)
    {
        app.UseResponseCompression();

        return app;
    }

    //--------------------------------------------------------------------------------
    // OpenApi
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureOpenApi(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenApi(static options =>
        {
            options.AddDocumentTransformer(static (document, _, _) =>
            {
                document.Info.Title = "POS API";
                document.Info.Version = "v1";
                document.Info.Description = "POS server API.";

                // 端末のトークン (Swagger UI の Authorize で試せる。管理向けはログイン中のブラウザの Cookie で通る)
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
                document.Components.SecuritySchemes[AuthSchemes.Terminal] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    Description = "端末のトークン (POST /api/v1/terminals/pair で受け取る)"
                };
                document.Security ??= [];
                document.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(AuthSchemes.Terminal, document)] = [] });
                return Task.CompletedTask;
            });
        });

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Blazor
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureBlazor(this IHostApplicationBuilder builder)
    {
        // Razor components
        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents();

        // 認証の状態 (回線の中でもアカウントの変更を反映する。既定の提供元を置き換えるので Razor components の後に登録する)
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<AuthenticationStateProvider, AccountAuthenticationStateProvider>();

        // Error boundary logging
        builder.Services.AddScoped<Microsoft.AspNetCore.Components.Web.IErrorBoundaryLogger, ErrorBoundaryLogger>();

        // 店舗フィルタ (回線ごとに共有)
        builder.Services.AddScoped<StoreFilterState>();

        // MudBlazor
        builder.Services.AddMudServices(static options =>
        {
            options.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
            options.SnackbarConfiguration.PreventDuplicates = true;
            options.SnackbarConfiguration.NewestOnTop = false;
            options.SnackbarConfiguration.ShowCloseIcon = true;
            options.SnackbarConfiguration.VisibleStateDuration = 5000;
            options.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
        });

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Health
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureHealth(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddHealthChecks()
            .AddCheck("self", static () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Telemetry
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureTelemetry(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = builder.Configuration.IsOtelExporterEnabled();

        var prometheusSection = builder.Configuration.GetSection("Prometheus");
        var prometheusUri = prometheusSection.GetValue<string>("Uri")!;
        var usePrometheusExporter = !String.IsNullOrEmpty(prometheusUri);

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(config =>
            {
                config.AddService(
                    serviceName: builder.Environment.ApplicationName,
                    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString(),
                    serviceInstanceId: Environment.MachineName);
            });

        // Log
        if (useOtlpExporter)
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });
            builder.Services.Configure<OpenTelemetryLoggerOptions>(static logging =>
            {
                logging.AddOtlpExporter();
            });
        }

        // Metrics
        if (useOtlpExporter || usePrometheusExporter)
        {
            telemetry
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddRuntimeInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddAspNetCoreInstrumentation()
                        .AddApplicationInstrumentation();

                    if (useOtlpExporter)
                    {
                        metrics.AddOtlpExporter();
                    }

                    if (usePrometheusExporter)
                    {
                        var prometheusEndpoint = new Uri(prometheusUri);
                        metrics.AddPrometheusHttpListener(config =>
                        {
                            config.Host = prometheusEndpoint.Host;
                            config.Port = prometheusEndpoint.Port;
                        });
                    }
                });
        }

        // Trace
        if (useOtlpExporter)
        {
            telemetry
                .WithTracing(tracing =>
                {
                    tracing
                        .AddSource(builder.Environment.ApplicationName)
                        .AddAspNetCoreInstrumentation(static options =>
                        {
                            options.Filter = static context =>
                            {
                                var path = context.Request.Path;
                                return !path.StartsWithSegments(AlivenessEndpointPath, StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWithSegments(HealthEndpointPath, StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWithSegments("/redoc", StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase);
                            };
                        })
                        .AddHttpClientInstrumentation()
                        .AddMiniDataProfilerInstrumentation()
                        .AddApplicationInstrumentation();

                    tracing.AddOtlpExporter();
                });
        }

        // Custom instrument
        builder.Services.AddApplicationInstrument();

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Components
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureComponents(this IHostApplicationBuilder builder)
    {
        // System
        builder.Services.AddSingleton(TimeProvider.System);

        // Data
        builder.Services.AddSingleton<IDbProvider>(static p =>
        {
            var configuration = p.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("Default");

            var listener = CreateProfileListener(p, p.GetRequiredService<ProfilerSetting>());
            if (listener is not null)
            {
                return new DelegateDbProvider(() => new ProfileDbConnection(listener, new SqliteConnection(connectionString)));
            }

            return new DelegateDbProvider(() => new SqliteConnection(connectionString));
        });
        builder.Services.AddSingleton<IDialect>(new DelegateDialect(
            static ex => ex is SqliteException { SqliteErrorCode: 19 } or SqliteException { SqliteExtendedErrorCode: 1555 or 2067 },
            static x => Regex.Replace(x, @"[%_\\]", @"\$0")));
        builder.Services.AddDataAccessors(typeof(DataProfile).Assembly);

        // Service
        builder.Services.AddSingleton(new DefaultPasswordProviderOptions());
        builder.Services.AddSingleton<IPasswordProvider, DefaultPasswordProvider>();
        builder.Services.AddCoreServices();
        builder.Services.AddSingleton<TerminalAccess>();

        // Report
        builder.Services.AddSingleton<ShiftReportBuilder>();
        builder.Services.AddSingleton<DailySalesReportBuilder>();
        builder.Services.AddSingleton<ReceiptReportBuilder>();
        builder.Services.AddSingleton<PurchaseOrderReportBuilder>();

        // Setting
        builder.Services.AddOptions<ProfilerSetting>().BindConfiguration("Profiler").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<ProfilerSetting>>().Value);
        builder.Services.AddOptions<LogSetting>().BindConfiguration("Log").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<LogSetting>>().Value);
        builder.Services.AddOptions<TelemetrySetting>().BindConfiguration("Telemetry").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<TelemetrySetting>>().Value);
        builder.Services.AddOptions<AuthSetting>().BindConfiguration("Auth").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<AuthSetting>>().Value);

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Information
    //--------------------------------------------------------------------------------

    public static void LogStartupInformation(this WebApplication app)
    {
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);

        var prometheusSection = app.Configuration.GetSection("Prometheus");
        var prometheusUri = prometheusSection.GetValue("Uri", string.Empty);

        app.Logger.InfoServiceStart();
        app.Logger.InfoServiceSettingsRuntime(RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription, RuntimeInformation.RuntimeIdentifier);
        app.Logger.InfoServiceSettingsEnvironment(typeof(Program).Assembly.GetName().Version, Environment.CurrentDirectory);
        app.Logger.InfoServiceSettingsGC(GCSettings.IsServerGC, GCSettings.LatencyMode, GCSettings.LargeObjectHeapCompactionMode);
        app.Logger.InfoServiceSettingsThreadPool(workerThreads, completionPortThreads);
        app.Logger.InfoServiceSettingsTelemetry(app.Configuration.GetOtelExporterEndpoint(), prometheusUri);
    }

    //--------------------------------------------------------------------------------
    // End point
    //--------------------------------------------------------------------------------

    public static WebApplication MapEndpoints(this WebApplication app)
    {
        // Develop
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            // [MEMO] Add yaml support
            app.MapOpenApi("/openapi/{documentName}.yaml");

            // NSwag UI (SwaggerUI / ReDoc) using MapOpenApi generated specification
            app.UseSwaggerUi(static options =>
            {
                options.DocumentPath = "/openapi/v1.json";
            });
            app.UseReDoc(static options =>
            {
                options.Path = "/redoc";
                options.DocumentPath = "/openapi/v1.json";
            });
        }

        // Static assets
        app.MapStaticAssets();

        // Blazor
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        // Health
        app.MapHealthChecks(HealthEndpointPath);
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = static r => r.Tags.Contains("live")
        });

        // Authentication (管理画面のログイン・ログアウト)
        app.MapAuthEndpoints();

        // API
        app.MapSettingsEndpoints();
        app.MapStoreEndpoints();
        app.MapTerminalEndpoints();
        app.MapStaffEndpoints();
        app.MapCategoryEndpoints();
        app.MapTaxRateEndpoints();
        app.MapProductEndpoints();
        app.MapDiscountEndpoints();
        app.MapPaymentMethodEndpoints();
        app.MapSyncEndpoints();
        app.MapCustomerEndpoints();
        app.MapTransactionEndpoints();
        app.MapShiftEndpoints();
        app.MapDailyClosingEndpoints();
        app.MapOrderEndpoints();
        app.MapInventoryEndpoints();
        app.MapAdjustmentReasonEndpoints();
        app.MapSupplierEndpoints();
        app.MapInventoryReceiptEndpoints();
        app.MapInventoryTransferEndpoints();
        app.MapPurchaseOrderEndpoints();
        app.MapReportEndpoints();

        return app;
    }

    //--------------------------------------------------------------------------------
    // Startup
    //--------------------------------------------------------------------------------

    public static async ValueTask InitializeApplicationAsync(this WebApplication app)
    {
        app.Services.GetRequiredService<ApplicationInstrument>();

        await app.Services.GetRequiredService<DatabaseService>().InitializeAsync(SchemaPath, InitialDataPath, CancellationToken.None);

        // アカウントがなければ初期の管理者を作る
        var setting = app.Services.GetRequiredService<AuthSetting>();
        await app.Services.GetRequiredService<AccountService>().InitializeAsync(setting.InitialName, setting.InitialPassword, CancellationToken.None);
    }

    //--------------------------------------------------------------------------------
    // Profiler
    //--------------------------------------------------------------------------------

    // Writes SQL traces to the log when enabled by settings
    private static IProfileListener? CreateProfileListener(IServiceProvider provider, ProfilerSetting setting)
    {
        var listeners = new List<IProfileListener>();
        if (setting.SqlLog.Enable)
        {
            var option = new LoggingListenerOption
            {
                OutputParameter = setting.SqlLog.OutputParameter,
                ElapsedThreshold = TimeSpan.FromMilliseconds(setting.SqlLog.ElapsedThresholdMilliseconds)
            };
            listeners.Add(new LoggingListener(provider.GetRequiredService<ILogger<LoggingListener>>(), option));
        }

        if (setting.SqlTelemetry.Enable)
        {
            listeners.Add(new OpenTelemetryListener(new OpenTelemetryListenerOption()));
        }

        return listeners.Count switch
        {
            0 => null,
            1 => listeners[0],
            _ => new ChainListener(listeners)
        };
    }

    //--------------------------------------------------------------------------------
    // Configuration
    //--------------------------------------------------------------------------------

    private static bool IsOtelExporterEnabled(this IConfiguration configuration) =>
        !String.IsNullOrWhiteSpace(configuration.GetOtelExporterEndpoint());

    private static string GetOtelExporterEndpoint(this IConfiguration configuration) =>
        configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? string.Empty;
}
