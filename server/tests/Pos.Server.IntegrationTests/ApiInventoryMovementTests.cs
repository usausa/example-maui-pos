namespace Pos.Server;

using System.Text;
using System.Text.Json;

using Pos.Contract.Inventory;
using Pos.Contract.InventoryReceipts;
using Pos.Contract.InventoryTransfers;
using Pos.Contract.Products;
using Pos.Contract.Staff;
using Pos.Contract.Suppliers;
using Pos.Server.Host.Endpoints;

// 入荷 (予定 → 受領で在庫に入る) と店舗間移動 (依頼 → 出荷で出荷店から減る → 受領で入荷店に増える)。状態に合わない操作は 422
public sealed class ApiInventoryMovementTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    private readonly JsonSerializerOptions options;

    public ApiInventoryMovementTests(TestApplicationFactory factory)
    {
        this.factory = factory;
        options = factory.JsonOptions();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // 入荷は数えた数で在庫に入り (変動は Receive)、受領した後の受領・キャンセルは 422。入荷予定はキャンセルできる
    // 受領の明細の重複は 400、見つからない担当は 422 (入荷がないことと状態が合わないことを先に返す)
    [Fact]
    public async Task ReceiptAddsCountedQuantity()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var sdCardBefore = await QuantityAsync(client, TestData.MainStoreId, TestData.SdCardProductId);
        var cameraBefore = await QuantityAsync(client, TestData.MainStoreId, TestData.CameraProductId);
        var create = new InventoryReceiptCreateRequest
        {
            StoreId = TestData.MainStoreId,
            SupplierId = TestData.SupplierId,
            SlipNo = "D-1001",
            ExpectedDate = new DateOnly(2026, 9, 15),
            Lines =
            [
                new InventoryReceiptCreateRequestLine { ProductId = TestData.SdCardProductId, Quantity = 10m, Cost = 1200m },
                new InventoryReceiptCreateRequestLine { ProductId = TestData.CameraProductId, Quantity = 2m, Cost = 60000m }
            ]
        };

        // Act / Assert: 登録すると入荷予定になり、明細は商品の写しを持つ
        using var createResponse = await client.PostJsonAsync(ApiRoutes.InventoryReceipts, create, options);
        var receipt = await createResponse.ReadAsAsync<InventoryReceiptListResponseItem>(HttpStatusCode.Created, options);
        var supplier = await client.GetJsonAsync<SupplierListResponseItem>($"{ApiRoutes.Suppliers}/{TestData.SupplierId}", options);
        Assert.Equal((InventoryReceiptStatus.Draft, supplier.Name), (receipt.Status, receipt.SupplierName));
        Assert.Equal(["SD-64", "CAM-X100"], receipt.Lines.Select(static x => x.ProductCode));
        var drafts = await client.GetJsonAsync<InventoryReceiptListResponse>($"{ApiRoutes.InventoryReceipts}?storeId={TestData.MainStoreId}&status=Draft", options);
        Assert.Contains(drafts.Items, x => x.Id == receipt.Id);

        // Act / Assert: 明細が重複している・null なら 400、担当が見つからなければ 422 (どれも受領しない)
        var duplicated = new InventoryReceiptReceiveRequest
        {
            StaffId = TestData.MainCashierStaffId,
            Lines =
            [
                new InventoryReceiptReceiveRequestLine { LineId = receipt.Lines[0].Id, Quantity = 9m },
                new InventoryReceiptReceiveRequestLine { LineId = receipt.Lines[0].Id, Quantity = 8m }
            ]
        };
        using var duplicatedResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryReceipts}/{receipt.Id}/receive", duplicated, options);
        await duplicatedResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var nullLinesContent = new StringContent("""{"lines":null}""", Encoding.UTF8, "application/json");
        using var nullLinesResponse = await client.PostAsync(new Uri($"{ApiRoutes.InventoryReceipts}/{receipt.Id}/receive", UriKind.Relative), nullLinesContent, Token);
        await nullLinesResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var noStaffResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryReceipts}/{receipt.Id}/receive", new InventoryReceiptReceiveRequest { StaffId = Guid.NewGuid() }, options);
        await noStaffResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "STAFF_INVALID", options);

        // Act / Assert: SD カードは 9 個だけ届いた (カメラは予定どおり)
        var receive = new InventoryReceiptReceiveRequest
        {
            StaffId = TestData.MainCashierStaffId,
            Lines = [new InventoryReceiptReceiveRequestLine { LineId = receipt.Lines[0].Id, Quantity = 9m }]
        };
        using var receiveResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryReceipts}/{receipt.Id}/receive", receive, options);
        var received = await receiveResponse.ReadAsAsync<InventoryReceiptListResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal((InventoryReceiptStatus.Received, 9m, 2m), (received.Status, received.Lines[0].ReceivedQuantity, received.Lines[1].ReceivedQuantity));
        Assert.Equal(sdCardBefore + 9m, await QuantityAsync(client, TestData.MainStoreId, TestData.SdCardProductId));
        Assert.Equal(cameraBefore + 2m, await QuantityAsync(client, TestData.MainStoreId, TestData.CameraProductId));
        var changes = await client.GetJsonAsync<InventoryChangeListResponse>($"{ApiRoutes.Inventory}/changes?storeId={TestData.MainStoreId}&productId={TestData.SdCardProductId}&type=Receive", options);
        var change = changes.Items.Single(x => x.ReferenceId == receipt.Id);
        Assert.Equal((9m, "D-1001", "InventoryReceipt"), (change.QuantityDelta, change.Reason, change.ReferenceType));

        // Act / Assert: 受領した後の受領とキャンセルは 422、ない入荷は 404 (担当が見つからなくても)
        using var againResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryReceipts}/{receipt.Id}/receive", receive, options);
        await againResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "INVENTORY_RECEIPT_STATUS_INVALID", options);
        using var againNoStaffResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryReceipts}/{receipt.Id}/receive", new InventoryReceiptReceiveRequest { StaffId = Guid.NewGuid() }, options);
        await againNoStaffResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "INVENTORY_RECEIPT_STATUS_INVALID", options);
        using var cancelReceivedResponse = await client.PostAsync(new Uri($"{ApiRoutes.InventoryReceipts}/{receipt.Id}/cancel", UriKind.Relative), null, Token);
        await cancelReceivedResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "INVENTORY_RECEIPT_STATUS_INVALID", options);
        using var missingResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryReceipts}/{Guid.NewGuid()}/receive", receive, options);
        await missingResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);
        using var missingNoStaffResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryReceipts}/{Guid.NewGuid()}/receive", new InventoryReceiptReceiveRequest { StaffId = Guid.NewGuid() }, options);
        await missingNoStaffResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);

        // Act / Assert: 入荷予定はキャンセルでき、仕入先がなければ登録できない
        using var draftResponse = await client.PostJsonAsync(ApiRoutes.InventoryReceipts, create, options);
        var draft = await draftResponse.ReadAsAsync<InventoryReceiptListResponseItem>(HttpStatusCode.Created, options);
        using var cancelResponse = await client.PostAsync(new Uri($"{ApiRoutes.InventoryReceipts}/{draft.Id}/cancel", UriKind.Relative), null, Token);
        Assert.Equal(InventoryReceiptStatus.Cancelled, (await cancelResponse.ReadAsAsync<InventoryReceiptListResponseItem>(HttpStatusCode.OK, options)).Status);
        create.SupplierId = Guid.NewGuid();
        using var unknownResponse = await client.PostJsonAsync(ApiRoutes.InventoryReceipts, create, options);
        await unknownResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
    }

    // 移動は出荷で出荷店から依頼の数だけ減り、受領で入荷店に数えた数だけ増える。出荷の前の受領と出荷の後のキャンセルは 422
    // 見つからない担当は 422 (出荷は管理の入力なので削除済みも)、受領の明細の重複は 400
    [Fact]
    public async Task TransferMovesStockBetweenStores()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var fromBefore = await QuantityAsync(client, TestData.MainStoreId, TestData.SdCardProductId);
        var toBefore = await QuantityAsync(client, TestData.BranchStoreId, TestData.SdCardProductId);
        var create = new InventoryTransferCreateRequest
        {
            FromStoreId = TestData.MainStoreId,
            ToStoreId = TestData.BranchStoreId,
            Note = "支店の品切れ",
            Lines = [new InventoryTransferCreateRequestLine { ProductId = TestData.SdCardProductId, Quantity = 3m }]
        };
        using var staffResponse = await client.PostJsonAsync(ApiRoutes.Staff, new StaffCreateRequest { Code = $"T{Guid.NewGuid():N}"[..10], Name = "削除済み", Role = StaffRole.Cashier, StoreId = TestData.MainStoreId }, options);
        var deletedStaff = await staffResponse.ReadAsAsync<StaffListResponseItem>(HttpStatusCode.Created, options);
        using var deleteStaffResponse = await client.DeleteUrlAsync($"{ApiRoutes.Staff}/{deletedStaff.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteStaffResponse.StatusCode);

        // Act / Assert: 同じ店舗への移動は 400。依頼は出荷店ごとの移動番号を持つ
        using var sameStoreResponse = await client.PostJsonAsync(ApiRoutes.InventoryTransfers, new InventoryTransferCreateRequest { FromStoreId = TestData.MainStoreId, ToStoreId = TestData.MainStoreId, Lines = create.Lines }, options);
        await sameStoreResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var createResponse = await client.PostJsonAsync(ApiRoutes.InventoryTransfers, create, options);
        var transfer = await createResponse.ReadAsAsync<InventoryTransferListResponseItem>(HttpStatusCode.Created, options);
        Assert.StartsWith("S001-T-", transfer.TransferNo, StringComparison.Ordinal);
        Assert.Equal((InventoryTransferStatus.Requested, "本店", "支店"), (transfer.Status, transfer.FromStoreName, transfer.ToStoreName));
        var receive = new InventoryTransferReceiveRequest { StaffId = TestData.BranchCashierStaffId, Lines = [new InventoryTransferReceiveRequestLine { LineId = transfer.Lines[0].Id, Quantity = 2m }] };
        using var earlyResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryTransfers}/{transfer.Id}/receive", receive, options);
        await earlyResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "INVENTORY_TRANSFER_STATUS_INVALID", options);
        using var earlyNoStaffResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryTransfers}/{transfer.Id}/receive", new InventoryTransferReceiveRequest { StaffId = Guid.NewGuid() }, options);
        await earlyNoStaffResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "INVENTORY_TRANSFER_STATUS_INVALID", options);

        // Act / Assert: 担当が見つからない・削除済みの出荷は 422 で、在庫を動かさない
        using var noStaffShipResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryTransfers}/{transfer.Id}/ship", new InventoryTransferShipRequest { StaffId = Guid.NewGuid() }, options);
        await noStaffShipResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "STAFF_INVALID", options);
        using var deletedStaffShipResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryTransfers}/{transfer.Id}/ship", new InventoryTransferShipRequest { StaffId = deletedStaff.Id }, options);
        await deletedStaffShipResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "STAFF_INVALID", options);
        Assert.Equal(fromBefore, await QuantityAsync(client, TestData.MainStoreId, TestData.SdCardProductId));

        // Act / Assert: 出荷で出荷店の在庫が減り、キャンセルはできなくなる
        using var shipResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryTransfers}/{transfer.Id}/ship", new InventoryTransferShipRequest(), options);
        Assert.Equal(InventoryTransferStatus.Shipped, (await shipResponse.ReadAsAsync<InventoryTransferListResponseItem>(HttpStatusCode.OK, options)).Status);
        Assert.Equal(fromBefore - 3m, await QuantityAsync(client, TestData.MainStoreId, TestData.SdCardProductId));
        using var cancelResponse = await client.PostAsync(new Uri($"{ApiRoutes.InventoryTransfers}/{transfer.Id}/cancel", UriKind.Relative), null, Token);
        await cancelResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "INVENTORY_TRANSFER_STATUS_INVALID", options);
        var open = await client.GetJsonAsync<InventoryTransferListResponse>($"{ApiRoutes.InventoryTransfers}?toStoreId={TestData.BranchStoreId}&open=true", options);
        Assert.Contains(open.Items, x => x.Id == transfer.Id);

        // Act / Assert: 受領の明細の重複と null は 400、担当が見つからなければ 422 (受領しない)
        var duplicated = new InventoryTransferReceiveRequest
        {
            StaffId = TestData.BranchCashierStaffId,
            Lines =
            [
                new InventoryTransferReceiveRequestLine { LineId = transfer.Lines[0].Id, Quantity = 2m },
                new InventoryTransferReceiveRequestLine { LineId = transfer.Lines[0].Id, Quantity = 1m }
            ]
        };
        using var duplicatedResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryTransfers}/{transfer.Id}/receive", duplicated, options);
        await duplicatedResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var nullLinesContent = new StringContent("""{"lines":null}""", Encoding.UTF8, "application/json");
        using var nullLinesResponse = await client.PostAsync(new Uri($"{ApiRoutes.InventoryTransfers}/{transfer.Id}/receive", UriKind.Relative), nullLinesContent, Token);
        await nullLinesResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var noStaffReceiveResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryTransfers}/{transfer.Id}/receive", new InventoryTransferReceiveRequest { StaffId = Guid.NewGuid() }, options);
        await noStaffReceiveResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "STAFF_INVALID", options);
        Assert.Equal(toBefore, await QuantityAsync(client, TestData.BranchStoreId, TestData.SdCardProductId));

        // Act / Assert: 2 個だけ届いた。入荷店に数えた数だけ入り、変動は TransferIn
        using var receiveResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryTransfers}/{transfer.Id}/receive", receive, options);
        var received = await receiveResponse.ReadAsAsync<InventoryTransferListResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal((InventoryTransferStatus.Received, 2m), (received.Status, received.Lines[0].ReceivedQuantity));
        Assert.Equal(toBefore + 2m, await QuantityAsync(client, TestData.BranchStoreId, TestData.SdCardProductId));
        var changes = await client.GetJsonAsync<InventoryChangeListResponse>($"{ApiRoutes.Inventory}/changes?productId={TestData.SdCardProductId}&type=TransferIn", options);
        Assert.Equal((2m, transfer.TransferNo), changes.Items.Where(x => x.ReferenceId == transfer.Id).Select(static x => (x.QuantityDelta, x.Reason)).Single());
        open = await client.GetJsonAsync<InventoryTransferListResponse>($"{ApiRoutes.InventoryTransfers}?toStoreId={TestData.BranchStoreId}&open=true", options);
        Assert.DoesNotContain(open.Items, x => x.Id == transfer.Id);
    }

    // 入荷予定と移動の依頼は管理の入力なので、削除済みの商品は見つからないものとして 422
    [Fact]
    public async Task CreateRejectsDeletedProduct()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        using var createProductResponse = await client.PostJsonAsync(ApiRoutes.Products, new ProductCreateRequest { Code = $"T{Guid.NewGuid():N}"[..10], Name = "削除済み", CategoryId = TestData.AccessoryCategoryId, Kind = ProductKind.Goods, Price = 100m, TaxRateId = TestData.StandardTaxRateId }, options);
        var deletedProduct = await createProductResponse.ReadAsAsync<ProductListResponseItem>(HttpStatusCode.Created, options);
        using var deleteProductResponse = await client.DeleteUrlAsync($"{ApiRoutes.Products}/{deletedProduct.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteProductResponse.StatusCode);

        // Act
        using var receiptResponse = await client.PostJsonAsync(ApiRoutes.InventoryReceipts, new InventoryReceiptCreateRequest { StoreId = TestData.MainStoreId, SupplierId = TestData.SupplierId, Lines = [new InventoryReceiptCreateRequestLine { ProductId = deletedProduct.Id, Quantity = 1m, Cost = 50m }] }, options);
        using var transferResponse = await client.PostJsonAsync(ApiRoutes.InventoryTransfers, new InventoryTransferCreateRequest { FromStoreId = TestData.MainStoreId, ToStoreId = TestData.BranchStoreId, Lines = [new InventoryTransferCreateRequestLine { ProductId = deletedProduct.Id, Quantity = 1m }] }, options);

        // Assert
        await receiptResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "PRODUCT_NOT_FOUND", options);
        await transferResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "PRODUCT_NOT_FOUND", options);
    }

    // 入荷・移動の変動は伝票の受領・出荷で作るので、変動の登録 API では受け付けない (400)
    [Fact]
    public async Task ChangesRejectMovementTypes()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var request = new InventoryChangesRequest
        {
            Changes =
            [
                new InventoryChangesRequestChange
                {
                    Id = Guid.NewGuid(),
                    StoreId = TestData.MainStoreId,
                    ProductId = TestData.SdCardProductId,
                    Type = InventoryChangeType.Receive,
                    Quantity = 5m,
                    StaffId = TestData.MainCashierStaffId,
                    OccurredAt = new DateTime(2026, 9, 11, 3, 15, 0, DateTimeKind.Utc)
                }
            ]
        };

        // Act
        using var response = await client.PostJsonAsync($"{ApiRoutes.Inventory}/changes", request, options);

        // Assert
        await response.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private async Task<decimal> QuantityAsync(HttpClient client, Guid storeId, Guid productId)
    {
        var levels = await client.GetJsonAsync<InventoryProductLevelsResponse>($"{ApiRoutes.Inventory}/{productId}", options);
        return levels.Levels.FirstOrDefault(x => x.StoreId == storeId)?.Quantity ?? 0m;
    }
}
