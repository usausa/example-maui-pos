namespace Pos.Server;

using System.Collections.Concurrent;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using Pos.Contract.Inventory;
using Pos.Contract.Shifts;
using Pos.Server.Host.Endpoints;
using Pos.Server.Services;

// 変更の通知: 書き込みを管理画面に知らせて読み直させる
public sealed class ChangeNotificationTests : IClassFixture<TestApplicationFactory>
{
    private static readonly DateTime Now = new(2026, 9, 14, 1, 0, 0, DateTimeKind.Utc);

    private readonly TestApplicationFactory factory;

    private readonly JsonSerializerOptions options;

    public ChangeNotificationTests(TestApplicationFactory factory)
    {
        this.factory = factory;
        options = factory.JsonOptions();
    }

    // シフトと在庫を書くと変更を通知し、受け付けなかった書き込みは通知しない
    [Fact]
    public async Task WritesNotifyChanges()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var kinds = new ConcurrentQueue<DataChangeKind>();
        var notification = factory.Services.GetRequiredService<ChangeNotificationService>();
        notification.Changed += (_, e) => kinds.Enqueue(e.Kind);

        // Act
        var open = new ShiftOpenRequest { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, TerminalId = TestData.MainTerminal2Id, BusinessDate = new DateOnly(2026, 9, 14), OpenedAt = Now, OpenedByStaffId = TestData.MainCashierStaffId, OpeningCash = 0m };
        using var openResponse = await client.PostJsonAsync(ApiRoutes.Shifts, open, options);
        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);
        var again = new ShiftOpenRequest { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, TerminalId = TestData.MainTerminal2Id, BusinessDate = new DateOnly(2026, 9, 14), OpenedAt = Now, OpenedByStaffId = TestData.MainCashierStaffId, OpeningCash = 0m };
        using var againResponse = await client.PostJsonAsync(ApiRoutes.Shifts, again, options);
        await againResponse.ReadProblemAsync(HttpStatusCode.Conflict, "TERMINAL_HAS_OPEN_SHIFT", options);
        var change = new InventoryChangesRequest
        {
            Changes = [new InventoryChangesRequestChange { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, ProductId = TestData.SdCardProductId, Type = InventoryChangeType.Adjustment, Quantity = 1m, Reason = "テスト", StaffId = TestData.MainCashierStaffId, OccurredAt = Now }]
        };
        using var changeResponse = await client.PostJsonAsync($"{ApiRoutes.Inventory}/changes", change, options);
        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        // Assert
        Assert.Equal([DataChangeKind.Shift, DataChangeKind.Inventory], kinds.ToArray());
    }
}
