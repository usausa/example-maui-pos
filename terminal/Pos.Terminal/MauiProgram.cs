namespace Pos.Terminal;

using System.Net.Http.Headers;

using BarcodeScanning;

using BunnyTail.DependencyInjection;

using CommunityToolkit.Maui;

using Fonts;

using Microsoft.Data.Sqlite;
using Microsoft.Maui.LifecycleEvents;

#if false
using Plugin.Maui.DebugRainbows;
#endif

using Pos.Terminal.Behaviors;
using Pos.Terminal.Components;
using Pos.Terminal.Extender;
using Pos.Terminal.Helpers;
using Pos.Terminal.Modules;
using Pos.Terminal.Modules.Dialogs;
using Pos.Terminal.Modules.Inquiry;
using Pos.Terminal.Modules.Inventory;
using Pos.Terminal.Modules.Returns;
using Pos.Terminal.Modules.Sales;

using SkiaSharp.Views.Maui.Controls.Hosting;

using Smart.Data;
using Smart.Data.Accessor.Attributes;

using Smart.Mvvm.Resolver;

using Syncfusion.Maui.Toolkit.Hosting;

public static partial class MauiProgram
{
    private const string ModulesNamespace = "Pos.Terminal.Modules";

    public static MauiApp CreateMauiApp() =>
        MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .UseGeneratedServiceProvider()
            .ConfigureDebug()
            .ConfigureFonts(ConfigureFonts)
            .ConfigureLifecycleEvents(ConfigureLifecycleEvents)
            .ConfigureEssentials(ConfigureEssentials)
            .ConfigureLogging()
            .ConfigureGlobalSettings()
            .ConfigureSyncfusionToolkit()
            .UseSkiaSharp()
            .UseBarcodeScanning()
            .UseMauiCommunityToolkit(ConfigureMauiCommunityToolkit)
            .UseMauiServices()
            .UseMauiComponents()
            .UseCommunityToolkitServices()
            .UseCustomView()
            .BuildApplication();

    // ------------------------------------------------------------
    // Debug
    // ------------------------------------------------------------

    private static MauiAppBuilder ConfigureDebug(this MauiAppBuilder builder)
    {
#if DEBUG
#if false
        builder
            .UseDebugRainbows(new DebugRainbowsOptions
            {
                ShowRainbows = true,
                ShowGrid = true,
                HorizontalItemSize = 20,
                VerticalItemSize = 20,
                MajorGridLineInterval = 4,
                MajorGridLines = new GridLineOptions { Color = Color.FromRgb(255, 0, 0), Opacity = 0.5, Width = 3 },
                MinorGridLines = new GridLineOptions { Color = Color.FromRgb(255, 0, 0), Opacity = 0.25, Width = 1 },
                GridOrigin = DebugGridOrigin.TopLeft
            });
#endif
#endif
        return builder;
    }

    // ------------------------------------------------------------
    // Logging
    // ------------------------------------------------------------

    private static MauiAppBuilder ConfigureLogging(this MauiAppBuilder builder)
    {
        // Debug
#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Android
#if ANDROID
        builder.Logging.AddAndroidLogger(static options => options.ShortCategory = true);
#endif
        // File
        builder.Logging.AddFileLogger(static options =>
            {
#if ANDROID
                options.Directory = Path.Combine(AndroidHelper.GetExternalFilesDir(), "log");
#endif
                options.RetainDays = 7;
            })
            .AddFilter(typeof(MauiProgram).Namespace, LogLevel.Debug);

        return builder;
    }

    // ------------------------------------------------------------
    // Application
    // ------------------------------------------------------------

    // ReSharper disable UnusedParameter.Local
    private static void ConfigureLifecycleEvents(ILifecycleBuilder effects)
    {
    }
    // ReSharper restore UnusedParameter.Local

    // ReSharper disable UnusedParameter.Local
    private static void ConfigureEssentials(IEssentialsBuilder config)
    {
    }
    // ReSharper restore UnusedParameter.Local

    private static void ConfigureMauiCommunityToolkit(Options options)
    {
        // ポップアップは画面の下端に寄せたシート (幅いっぱい)。上角の丸みは表示のたびに CreatePopupOptions で付ける
        options.SetPopupDefaults(new DefaultPopupSettings
        {
            CanBeDismissedByTappingOutsideOfPopup = false,
            Padding = 0,
            Margin = 0,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.End
        });
        options.SetPopupOptionsDefaults(new DefaultPopupOptionsSettings
        {
            CanBeDismissedByTappingOutsideOfPopup = false,
            Shadow = null,
            Shape = null
        });
    }

    private static MauiAppBuilder ConfigureGlobalSettings(this MauiAppBuilder builder)
    {
        // TODO App center alternative

        // Crash dump
        CrashReport.Start();

        return builder;
    }

    private static MauiAppBuilder UseCustomView(this MauiAppBuilder builder)
    {
        // Behaviors
        builder.ConfigureCustomBehaviors(static options =>
        {
            options.HandleEnterKey = true;
            options.DisableShowSoftInputOnFocus = true;
        });

        return builder;
    }

    // ------------------------------------------------------------
    // Design
    // ------------------------------------------------------------

    private static void ConfigureFonts(IFontCollection fonts)
    {
        fonts.AddFont("MaterialIcons-Regular.ttf", MaterialIcons.FontFamily);
    }

    private static void ConfigureDialogDesign(DialogConfig config)
    {
        var resources = Application.Current!.Resources;
        config.IndicatorColor = resources.FindResource<Color>("BlueAccent2");
        config.LoadingMessageFontSize = 28;
        config.ProgressCircleColor1 = resources.FindResource<Color>("BlueAccent2");
        config.ProgressCircleColor2 = resources.FindResource<Color>("GrayLighten2");

        // Avoiding conflicts with progress
        config.LockBackgroundColor = Colors.Transparent;
        config.LoadingBackgroundColor = Colors.Transparent;
        config.ProgressBackgroundColor = Colors.Transparent;
    }

    // シートの上角を丸める (Shape は要素なので表示のたびに作る)。一覧からの選択だけは外側のタップでも閉じる
    private static PopupOptions CreatePopupOptions(bool dismissByTappingOutside) => new()
    {
        CanBeDismissedByTappingOutsideOfPopup = dismissByTappingOutside,
        // Shapes 名前空間は Path が System.IO.Path と衝突するので完全修飾する
        Shape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16, 16, 0, 0), StrokeThickness = 0 }
    };

    // ------------------------------------------------------------
    // Components
    // ------------------------------------------------------------

    private static MauiAppBuilder UseGeneratedServiceProvider(this MauiAppBuilder builder)
    {
        builder.ConfigureContainer(
            new GeneratedServiceProviderFactory(static options => options.TrackTransientDisposables = false),
            ConfigureComponents);
        return builder;
    }

    private static void ConfigureComponents(IServiceCollection services)
    {
        // View & ViewModel
        services.AddTransient<MainPage>();
        services.AddTransient<MainPageViewModel>();
        services.AddViews();
        services.AddViewModels();

        // MauiComponents
        // 確認と情報のダイアログはシート (SheetDialog)。それ以外は標準の DialogImplementation に委ねる
        services.AddSingleton<DialogImplementation>();
        services.AddSingleton<IDialog, SheetDialog>();
        services.AddComponentsDialog(static c =>
        {
            ConfigureDialogDesign(c);
            c.EnablePromptEnterAction = true;
            c.EnablePromptSelectAll = true;
        });
        services.AddComponentsPopup(static c =>
        {
            c.AutoRegister(DialogSource());
            c.OptionFactory = static (_, id) => CreatePopupOptions(id is DialogId.Select);
        });
        services.AddComponentsScreen();
        services.AddComponentsLocation();
        services.AddComponentsSpeech();
        services.AddCommunication();

        // Messenger
        services.AddSingleton<IReactiveMessenger>(ReactiveMessenger.Default);

        // Navigator
        services.AddNavigator(static (_, config) =>
        {
            config.UseMauiNavigationProvider();
            config.AddHierarchyEffectPlugin();
            config.AddPlugin<NavigationFeedbackPlugin>();
            config.UseIdViewMapper(static m => m.AutoRegister(ViewSource()));
        });

        // Components
        services.AddSingleton<IStorageManager, StorageManager>();

        // Resource
        services.AddSingleton<ResourceDictionary>(static _ => Application.Current!.Resources);

        // State
        services.AddSingleton(BusyState.Default);
        services.AddSingleton<StartupState>();
        services.AddSingleton<DeviceState>();
        services.AddSingleton<Settings>();
        services.AddSingleton<Session>();

        // HttpClient
        services
            .AddHttpClient(ApiNames.Default, SetupHttpClient)
            .ConfigurePrimaryHttpMessageHandler(CreateHttpMessageHandler);
        services.AddSingleton<ApiContext>();

        // Service
        services.AddSingleton<IDbProvider>(static p =>
        {
            var storage = p.GetRequiredService<IStorageManager>();
            var path = Path.Combine(storage.PrivateFolder, "pos.db");
            return new DelegateDbProvider(() => new SqliteConnection($"Data Source={path};Default Timeout=10"));
        });
        services.AddDataAccessors();
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<HttpService>();
        services.AddSingleton<NetworkService>();
        services.AddSingleton<CredentialService>();
        services.AddSingleton<PinService>();
        services.AddSingleton<SyncService>();
        services.AddSingleton<ReceiptService>();
        services.AddSingleton<ProductImageService>();

        // Usecase
        services.AddSingleton<TransactionUsecase>();
        services.AddSingleton<SalesUsecase>();
        services.AddSingleton<ReturnUsecase>();
        services.AddSingleton<ShiftUsecase>();
        services.AddSingleton<StockUsecase>();
        services.AddSingleton<SetupUsecase>();
        services.AddSingleton<OrderUsecase>();
        services.AddSingleton<ReceivingUsecase>();

        // Scope (画面間で共有する状態。Navigator の Scope プラグインが生成し、参照する画面がなくなると破棄する)
        services.AddTransient<SalesContext>();
        services.AddTransient<ReturnContext>();
        services.AddTransient<StockContext>();
        services.AddTransient<ReceivingContext>();
        services.AddTransient<CustomerDraft>();
    }

    // ------------------------------------------------------------
    // Network
    // ------------------------------------------------------------

    private static void SetupHttpClient(IServiceProvider provider, HttpClient client)
    {
        client.BaseAddress = provider.GetRequiredService<ApiContext>().BaseAddress;
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
    }

    private static HttpMessageHandler CreateHttpMessageHandler() =>
        new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            // サーバに届かないときは 30 秒待たずに諦める (全体のタイムアウトは大きい同期のために残す)
            ConnectTimeout = TimeSpan.FromSeconds(5)
        };

    // ------------------------------------------------------------
    // Data
    // ------------------------------------------------------------

    // ReSharper disable once UnusedMethodReturnValue.Local
    [DataAccessorRegistration]
    private static partial IServiceCollection AddDataAccessors(this IServiceCollection services);

    // ------------------------------------------------------------
    // Build
    // ------------------------------------------------------------

    private static MauiApp BuildApplication(this MauiAppBuilder builder)
    {
        var app = builder.Build();

        var services = app.Services;

        // Setup provider
        ResolveProvider.Default.Provider = services;

#if DEBUG
        // Diagnostics for GeneratedServiceProvider
        if (services is GeneratedServiceProvider generatedProvider)
        {
            foreach (var line in BunnyTail.DependencyInjection.Diagnostics.ServiceFactoryReportExtensions.DescribeRuntimeFallbacks(generatedProvider).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
            {
                System.Diagnostics.Debug.WriteLine(line);
            }
        }
#endif

#if DEBUG
        // Setup navigator
        var navigator = services.GetRequiredService<INavigator>();
        navigator.Navigated += (_, args) =>
        {
            // for debug
            System.Diagnostics.Debug.WriteLine($"Navigated: [{args.Context.FromId}]->[{args.Context.ToId}] : stacked=[{navigator.StackedCount}] effect=[{args.Context.Parameter.Effect}]");
        };
#endif

        // 接続先 (設定 QR で投入済みなら)
        var settings = services.GetRequiredService<Settings>();
        if (!String.IsNullOrEmpty(settings.ApiEndPoint))
        {
            services.GetRequiredService<ApiContext>().BaseAddress = new Uri(settings.ApiEndPoint);
        }

        return app;
    }

    // ------------------------------------------------------------
    // View & ViewModel
    // ------------------------------------------------------------

    // ReSharper disable UnusedMethodReturnValue.Local
    [ComponentRegistration(Lifetime.Transient, "View$", Namespace = ModulesNamespace)]
    private static partial IServiceCollection AddViews(this IServiceCollection services);

    [ComponentRegistration(Lifetime.Transient, "ViewModel$", Namespace = ModulesNamespace)]
    private static partial IServiceCollection AddViewModels(this IServiceCollection services);
    // ReSharper restore UnusedMethodReturnValue.Local

    // ------------------------------------------------------------
    // Navigation
    // ------------------------------------------------------------

    [ViewSource]
    public static partial IEnumerable<KeyValuePair<ViewId, Type>> ViewSource();

    [PopupSource]
    public static partial IEnumerable<KeyValuePair<DialogId, Type>> DialogSource();
}
