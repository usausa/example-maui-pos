namespace Pos.Server;

using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Pos.Contract.Inventory;
using Pos.Contract.Settings;
using Pos.Contract.Shifts;
using Pos.Contract.Staff;
using Pos.Contract.Sync;
using Pos.Contract.Terminals;
using Pos.Contract.Transactions;
using Pos.Domain.Logic;
using Pos.Server.Accessors;
using Pos.Server.Host.Endpoints;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

using Smart.Data;

// 認証と認可: 管理画面のログイン (Cookie)、端末のトークン (Bearer)、役割と端末の一致、担当と承認者
public sealed class ApiAuthTests : IClassFixture<TestApplicationFactory>
{
    private static readonly DateTime Now = new(2026, 9, 25, 2, 0, 0, DateTimeKind.Utc);

    private static readonly DateOnly BusinessDate = new(2026, 9, 25);

    private readonly TestApplicationFactory factory;

    private readonly JsonSerializerOptions options;

    public ApiAuthTests(TestApplicationFactory factory)
    {
        this.factory = factory;
        options = factory.JsonOptions();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // ログインもトークンもない API の要求は 401
    [Fact]
    public async Task AnonymousApiIsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri(ApiRoutes.Stores, UriKind.Relative), Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // パスワードが違えばログイン画面に戻る
    [Fact]
    public async Task LoginWithWrongPasswordReturnsToLogin()
    {
        // Arrange
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        // Act
        using var response = await client.PostLoginAsync(AuthTestExtensions.AdminName, "wrong-password");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login?error=1", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    // オペレーターは参照と業務の操作はできるが、マスタ・会社設定・締めの解除は 403
    [Fact]
    public async Task OperatorCannotChangeMasters()
    {
        // Arrange
        var name = $"op{Guid.NewGuid():N}"[..12];
        await factory.Services.GetRequiredService<AccountService>().InsertAsync(name, "operator-password", AccountRole.Operator, Token);
        var client = await factory.CreateLoginClientAsync(name, "operator-password");
        var settings = await client.GetJsonAsync<SettingsResponse>(ApiRoutes.Settings, options);

        // Act
        using var productsCsv = await client.GetAsync(new Uri($"{ApiRoutes.Products}/csv", UriKind.Relative), Token);
        using var updateSettings = await client.PutJsonAsync(ApiRoutes.Settings, new SettingsUpdateRequest { CompanyName = settings.CompanyName, Currency = settings.Currency, TaxRounding = settings.TaxRounding, PointBasis = settings.PointBasis, BusinessDayStartTime = settings.BusinessDayStartTime, Version = settings.Version }, options);
        using var deleteTaxRate = await client.DeleteUrlAsync($"{ApiRoutes.TaxRates}/{TestData.StandardTaxRateId}");
        using var reopen = await client.DeleteUrlAsync($"{ApiRoutes.DailyClosings}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, productsCsv.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, updateSettings.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteTaxRate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reopen.StatusCode);
    }

    // アカウントを無効にすると、ログイン中のセッションも次の要求から使えない
    [Fact]
    public async Task DisabledAccountLosesSession()
    {
        // Arrange
        var accounts = factory.Services.GetRequiredService<AccountService>();
        var name = $"op{Guid.NewGuid():N}"[..12];
        await accounts.InsertAsync(name, "operator-password", AccountRole.Operator, Token);
        var client = await factory.CreateLoginClientAsync(name, "operator-password");
        using var before = await client.GetAsync(new Uri(ApiRoutes.Stores, UriKind.Relative), Token);
        var account = (await accounts.QueryAllAsync(Token)).Single(x => x.Name == name);

        // Act
        await accounts.UpdateAsync(account.Id, account.Role, false, account.Version, Guid.Empty, Token);
        using var after = await client.GetAsync(new Uri(ApiRoutes.Stores, UriKind.Relative), Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    // ペアリングコードは不一致・期限切れ・使用済みなら 422
    [Fact]
    public async Task PairingCodeIsSingleUseAndExpires()
    {
        // Arrange
        var client = factory.CreateClient();
        var code = await factory.Services.GetRequiredService<TerminalTokenService>().IssuePairingCodeAsync(TestData.BranchTerminalId, Token);
        await InsertExpiredPairingCodeAsync("999999");

        // Act
        using var paired = await client.PostJsonAsync($"{ApiRoutes.Terminals}/pair", new TerminalPairRequest { PairingCode = code!.Code, DeviceName = "test", AppVersion = "1.0.0" }, options);
        using var reused = await client.PostJsonAsync($"{ApiRoutes.Terminals}/pair", new TerminalPairRequest { PairingCode = code.Code, DeviceName = "test" }, options);
        using var expired = await client.PostJsonAsync($"{ApiRoutes.Terminals}/pair", new TerminalPairRequest { PairingCode = "999999", DeviceName = "test" }, options);

        // Assert
        var response = await paired.ReadAsAsync<TerminalPairResponse>(HttpStatusCode.OK, options);
        Assert.Equal(TestData.BranchTerminalId, response.Terminal.Id);
        Assert.Equal(TestData.BranchStoreId, response.Store.Id);
        Assert.Equal("1.0.0", response.Terminal.AppVersion);
        await reused.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "PAIRING_CODE_INVALID", options);
        await expired.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "PAIRING_CODE_INVALID", options);
    }

    // 登録を解除した端末のトークンは次の要求から 401。再ペアリングで使える
    [Fact]
    public async Task RevokedTokenIsUnauthorized()
    {
        // Arrange
        var client = await factory.CreateTerminalClientAsync(TestData.MainTerminal2Id);
        using var before = await client.GetAsync(new Uri(ApiRoutes.Stores, UriKind.Relative), Token);

        // Act
        await factory.Services.GetRequiredService<TerminalTokenService>().RevokeAsync(TestData.MainTerminal2Id, Token);
        using var after = await client.GetAsync(new Uri(ApiRoutes.Stores, UriKind.Relative), Token);
        var repaired = await factory.CreateTerminalClientAsync(TestData.MainTerminal2Id);
        using var again = await repaired.GetAsync(new Uri(ApiRoutes.Stores, UriKind.Relative), Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    // 端末は自店・自端末の操作だけ (本文・クエリの店舗・端末がトークンと違えば 403)。管理だけの API も 403
    [Fact]
    public async Task TerminalCannotActForOtherStore()
    {
        // Arrange
        var client = await factory.CreateTerminalClientAsync(TestData.BranchTerminalId);
        var open = new ShiftOpenRequest { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, TerminalId = TestData.MainTerminal1Id, BusinessDate = BusinessDate, OpenedAt = Now, OpenedByStaffId = TestData.ManagerStaffId, OpeningCash = 0m };
        var change = new InventoryChangeRequest
        {
            Changes = [new InventoryChangeRequestChange { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, ProductId = TestData.SdCardProductId, Type = InventoryChangeType.Adjustment, Quantity = 1m, StaffId = TestData.ManagerStaffId, OccurredAt = Now }]
        };

        // Act
        using var openResponse = await client.PostJsonAsync(ApiRoutes.Shifts, open, options);
        using var currentResponse = await client.GetAsync(new Uri($"{ApiRoutes.Shifts}/current?terminalId={TestData.MainTerminal1Id}", UriKind.Relative), Token);
        using var changeResponse = await client.PostJsonAsync($"{ApiRoutes.Inventory}/changes", change, options);
        using var shiftListResponse = await client.GetAsync(new Uri(ApiRoutes.Shifts, UriKind.Relative), Token);

        // Assert
        await openResponse.ReadProblemAsync(HttpStatusCode.Forbidden, "TERMINAL_MISMATCH", options);
        await currentResponse.ReadProblemAsync(HttpStatusCode.Forbidden, "TERMINAL_MISMATCH", options);
        await changeResponse.ReadProblemAsync(HttpStatusCode.Forbidden, "TERMINAL_MISMATCH", options);
        Assert.Equal(HttpStatusCode.Forbidden, shiftListResponse.StatusCode);
    }

    // 端末向けの同期にだけ PIN のハッシュが載る (初期データの PIN で照合できる)。heartbeat は端末のトークンで記録する (管理画面のログインは 403)
    [Fact]
    public async Task TerminalSyncCarriesPinHash()
    {
        // Arrange
        var terminal = await factory.CreateTerminalClientAsync(TestData.BranchTerminalId);
        var admin = await factory.CreateAdminClientAsync();

        // Act
        var masters = await terminal.GetJsonAsync<SyncMastersResponse>($"{ApiRoutes.Sync}/masters", options);
        var staff = await admin.GetJsonAsync<StaffResponseItem>($"{ApiRoutes.Staff}/{TestData.ManagerStaffId}", options);
        using var heartbeat = await terminal.PostJsonAsync($"{ApiRoutes.Terminals}/me/heartbeat", new TerminalHeartbeatRequest { AppVersion = "9.9.9" }, options);
        using var adminHeartbeat = await admin.PostJsonAsync($"{ApiRoutes.Terminals}/me/heartbeat", new TerminalHeartbeatRequest { AppVersion = "0.0.0" }, options);
        var seen = await admin.GetJsonAsync<TerminalResponseItem>($"{ApiRoutes.Terminals}/{TestData.BranchTerminalId}", options);

        // Assert
        var manager = masters.Staff.Single(static x => x.Id == TestData.ManagerStaffId);
        Assert.True(PinHasher.Verify("1111", manager.PinHash));
        Assert.False(PinHasher.Verify("0000", manager.PinHash));
        Assert.Null(staff.PinHash);
        Assert.Equal(HttpStatusCode.NoContent, heartbeat.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminHeartbeat.StatusCode);
        Assert.Equal("9.9.9", seen.AppVersion);
    }

    // 承認が必要な値引は店長以上の承認者が要る。レジ係の取消も店長以上の承認が要る
    [Fact]
    public async Task ApprovalIsRequiredForDiscountAndCashierVoid()
    {
        // Arrange
        var client = await factory.CreateTerminalClientAsync(TestData.MainTerminal1Id);
        var shiftId = await OpenShiftAsync(client);

        // Act
        using var withoutApproval = await PostSaleAsync(client, shiftId, "S001-01-000901", approvedBy: null, withDiscount: true);
        using var approved = await PostSaleAsync(client, shiftId, "S001-01-000902", approvedBy: TestData.ManagerStaffId, withDiscount: true);
        var sale = await approved.ReadAsAsync<TransactionResponseItem>(HttpStatusCode.Created, options);
        using var voidWithoutApproval = await client.PostJsonAsync($"{ApiRoutes.Transactions}/{sale.Id}/void", new TransactionVoidRequest { StaffId = TestData.MainCashierStaffId, Reason = "誤操作", VoidedAt = Now.AddMinutes(30) }, options);
        using var voidWithApproval = await client.PostJsonAsync($"{ApiRoutes.Transactions}/{sale.Id}/void", new TransactionVoidRequest { StaffId = TestData.MainCashierStaffId, ApprovedByStaffId = TestData.ManagerStaffId, Reason = "誤操作", VoidedAt = Now.AddMinutes(30) }, options);

        // Assert
        await withoutApproval.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "APPROVAL_REQUIRED", options);
        await voidWithoutApproval.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "APPROVAL_REQUIRED", options);
        Assert.Equal(TransactionStatus.Voided, (await voidWithApproval.ReadAsAsync<TransactionResponseItem>(HttpStatusCode.OK, options)).Status);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private async Task InsertExpiredPairingCodeAsync(string code)
    {
        var accessor = factory.Services.GetRequiredService<TerminalTokenAccessor>();
        await factory.Services.GetRequiredService<IDbProvider>().UsingTxAsync(async (_, tx) =>
        {
            await accessor.InsertAsync(tx, new TerminalTokenEntity { Id = Guid.NewGuid(), TerminalId = TestData.BranchTerminalId, PairingCode = code, PairingExpiresAt = DateTime.UtcNow.AddMinutes(-1), CreatedAt = DateTime.UtcNow.AddMinutes(-11) }, Token);
            await tx.CommitAsync(Token);
        }, Token);
    }

    private async Task<Guid> OpenShiftAsync(HttpClient client)
    {
        var request = new ShiftOpenRequest { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, TerminalId = TestData.MainTerminal1Id, BusinessDate = BusinessDate, OpenedAt = Now, OpenedByStaffId = TestData.MainCashierStaffId, OpeningCash = 0m };
        using var response = await client.PostJsonAsync(ApiRoutes.Shifts, request, options);
        return (await response.ReadAsAsync<ShiftResponseItem>(HttpStatusCode.Created, options)).Id;
    }

    // SD カード 2,000 円 (内税 10%、ポイントなし) をレジ係が現金で。withDiscount は明細値引「展示品 5%」(承認が必要)
    private async Task<HttpResponseMessage> PostSaleAsync(HttpClient client, Guid shiftId, string receiptNo, Guid? approvedBy, bool withDiscount)
    {
        var lineId = Guid.NewGuid();
        var sale = new TransactionCreateRequest
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Sale,
            Status = TransactionStatus.Completed,
            StoreId = TestData.MainStoreId,
            TerminalId = TestData.MainTerminal1Id,
            StaffId = TestData.MainCashierStaffId,
            ShiftId = shiftId,
            ReceiptNo = receiptNo,
            BusinessDate = BusinessDate,
            TransactedAt = Now.AddMinutes(10),
            Lines =
            [
                new TransactionCreateRequestLine { Id = lineId, LineNo = 1, ProductId = TestData.SdCardProductId, ProductCode = "SD-64", ProductName = "SD カード 64GB", CategoryId = TestData.AccessoryCategoryId, Kind = ProductKind.Goods, ListPrice = 2000m, UnitPrice = 2000m, Quantity = 1m, TaxRateId = TestData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0m }
            ],
            Discounts = withDiscount
                ? [new TransactionCreateRequestDiscount { Id = Guid.NewGuid(), LineId = lineId, DiscountId = TestData.DisplayDiscountId, Name = "展示品 5%", Type = DiscountType.Percent, Value = 0.05m, ApprovedByStaffId = approvedBy }]
                : [],
            Payments =
            [
                new TransactionCreateRequestPayment { Id = Guid.NewGuid(), SeqNo = 1, PaymentMethodId = TestData.CashPaymentMethodId, Kind = PaymentKind.Cash, Amount = 1900m, TenderedAmount = 1900m }
            ]
        };
        using var calculateResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/calculate", TransactionRequests.ToCalculateRequest(sale), options);
        TransactionRequests.Apply(sale, await calculateResponse.ReadAsAsync<TransactionCalculateResponse>(HttpStatusCode.OK, options));
        return await client.PostJsonAsync(ApiRoutes.Transactions, sale, options);
    }
}

// 認証を無効にしたサーバは、ログインもトークンもない要求を通す
public sealed class ApiAuthDisabledTests : IClassFixture<AuthDisabledApplicationFactory>
{
    private readonly AuthDisabledApplicationFactory factory;

    public ApiAuthDisabledTests(AuthDisabledApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task AnonymousApiIsAllowed()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri(ApiRoutes.Stores, UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
