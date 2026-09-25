namespace Pos.Server;

using System.Text;
using System.Text.Json;

using Pos.Contract.Inventory;
using Pos.Contract.InventoryReceipts;
using Pos.Contract.PurchaseOrders;
using Pos.Server.Host.Endpoints;

// 発注 (下書き → 発注で入荷予定を作る → 入荷予定の受領で入荷済み)。キャンセルは入荷予定と連動し、状態に合わない操作は 422
public sealed class ApiPurchaseOrderTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    private readonly JsonSerializerOptions options;

    public ApiPurchaseOrderTests(TestApplicationFactory factory)
    {
        this.factory = factory;
        options = factory.JsonOptions();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // 下書きは変更でき、発注すると明細を写した入荷予定ができる。入荷予定を受領すると入荷済みになり、受領した数が発注の明細に出る
    [Fact]
    public async Task OrderCreatesReceiptAndReceivingCompletesOrder()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var sdCardBefore = await QuantityAsync(client, TestData.MainStoreId, TestData.SdCardProductId);
        var create = new PurchaseOrderCreateRequest
        {
            StoreId = TestData.MainStoreId,
            SupplierId = TestData.SupplierId,
            ExpectedDate = new DateOnly(2026, 9, 30),
            Note = "定番品の補充",
            Lines = [new PurchaseOrderCreateRequestLine { ProductId = TestData.SdCardProductId, Quantity = 5m, Cost = 1200m }]
        };

        // Act / Assert: 登録すると下書きで、店舗ごとの発注番号を持つ
        using var createResponse = await client.PostJsonAsync(ApiRoutes.PurchaseOrders, create, options);
        var draft = await createResponse.ReadAsAsync<PurchaseOrderResponseItem>(HttpStatusCode.Created, options);
        Assert.StartsWith("S001-P-", draft.PurchaseOrderNo, StringComparison.Ordinal);
        Assert.Equal((PurchaseOrderStatus.Draft, 6000m, null), (draft.Status, draft.TotalCost, draft.ReceiptId));

        // Act / Assert: 下書きは明細ごと変更でき、版が合わなければ 409
        var update = new PurchaseOrderUpdateRequest
        {
            SupplierId = TestData.SupplierId,
            ExpectedDate = create.ExpectedDate,
            Note = create.Note,
            Lines =
            [
                new PurchaseOrderUpdateRequestLine { ProductId = TestData.SdCardProductId, Quantity = 10m, Cost = 1200m },
                new PurchaseOrderUpdateRequestLine { ProductId = TestData.CameraProductId, Quantity = 1m }
            ],
            Version = draft.Version
        };
        using var updateResponse = await client.PutJsonAsync($"{ApiRoutes.PurchaseOrders}/{draft.Id}", update, options);
        var updated = await updateResponse.ReadAsAsync<PurchaseOrderResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal((2, 12000m), (updated.Lines.Count, updated.TotalCost));
        using var staleResponse = await client.PutJsonAsync($"{ApiRoutes.PurchaseOrders}/{draft.Id}", update, options);
        await staleResponse.ReadProblemAsync(HttpStatusCode.Conflict, "VERSION_MISMATCH", options);

        // Act / Assert: 発注すると明細を写した入荷予定ができ、入荷予定は発注番号を持つ。発注した後は変更できない
        using var orderResponse = await client.PostAsync(new Uri($"{ApiRoutes.PurchaseOrders}/{draft.Id}/order", UriKind.Relative), null, Token);
        var ordered = await orderResponse.ReadAsAsync<PurchaseOrderResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal((PurchaseOrderStatus.Ordered, "admin"), (ordered.Status, ordered.OrderedBy));
        var receipt = await client.GetJsonAsync<InventoryReceiptResponseItem>($"{ApiRoutes.InventoryReceipts}/{ordered.ReceiptId}", options);
        Assert.Equal((InventoryReceiptStatus.Draft, draft.Id, draft.PurchaseOrderNo), (receipt.Status, receipt.PurchaseOrderId, receipt.PurchaseOrderNo));
        Assert.Equal((10m, 1200m), (receipt.Lines[0].Quantity, receipt.Lines[0].Cost));
        Assert.Equal((1m, null), (receipt.Lines[1].Quantity, receipt.Lines[1].Cost));
        update.Version = ordered.Version;
        using var editOrderedResponse = await client.PutJsonAsync($"{ApiRoutes.PurchaseOrders}/{draft.Id}", update, options);
        await editOrderedResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "PURCHASE_ORDER_STATUS_INVALID", options);

        // Act / Assert: 入荷予定を受領すると発注は入荷済みになり、受領した数が明細に出る。入荷済みはキャンセルできない
        var receive = new InventoryReceiptReceiveRequest
        {
            StaffId = TestData.MainCashierStaffId,
            Lines = [new InventoryReceiptReceiveRequestLine { LineId = receipt.Lines[0].Id, Quantity = 8m }]
        };
        using var receiveResponse = await client.PostJsonAsync($"{ApiRoutes.InventoryReceipts}/{receipt.Id}/receive", receive, options);
        Assert.Equal(InventoryReceiptStatus.Received, (await receiveResponse.ReadAsAsync<InventoryReceiptResponseItem>(HttpStatusCode.OK, options)).Status);
        Assert.Equal(sdCardBefore + 8m, await QuantityAsync(client, TestData.MainStoreId, TestData.SdCardProductId));
        var completed = await client.GetJsonAsync<PurchaseOrderResponseItem>($"{ApiRoutes.PurchaseOrders}/{draft.Id}", options);
        Assert.Equal(PurchaseOrderStatus.Received, completed.Status);
        Assert.Equal([8m, 1m], completed.Lines.Select(static x => x.ReceivedQuantity ?? -1m));
        using var cancelReceivedResponse = await client.PostAsync(new Uri($"{ApiRoutes.PurchaseOrders}/{draft.Id}/cancel", UriKind.Relative), null, Token);
        await cancelReceivedResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "PURCHASE_ORDER_STATUS_INVALID", options);
    }

    // 発注済みのキャンセルは入荷予定もキャンセルし、入荷予定のキャンセルは発注もキャンセルする。キャンセルした発注は発注できない
    [Fact]
    public async Task CancelIsLinkedWithReceipt()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();

        // Act / Assert: 発注済みをキャンセルすると入荷予定もキャンセルになる
        var first = await OrderAsync(client, await CreateDraftAsync(client));
        using var cancelResponse = await client.PostAsync(new Uri($"{ApiRoutes.PurchaseOrders}/{first.Id}/cancel", UriKind.Relative), null, Token);
        Assert.Equal(PurchaseOrderStatus.Cancelled, (await cancelResponse.ReadAsAsync<PurchaseOrderResponseItem>(HttpStatusCode.OK, options)).Status);
        var firstReceipt = await client.GetJsonAsync<InventoryReceiptResponseItem>($"{ApiRoutes.InventoryReceipts}/{first.ReceiptId}", options);
        Assert.Equal(InventoryReceiptStatus.Cancelled, firstReceipt.Status);

        // Act / Assert: 入荷予定をキャンセルすると発注もキャンセルになる
        var second = await OrderAsync(client, await CreateDraftAsync(client));
        using var receiptCancelResponse = await client.PostAsync(new Uri($"{ApiRoutes.InventoryReceipts}/{second.ReceiptId}/cancel", UriKind.Relative), null, Token);
        Assert.Equal(InventoryReceiptStatus.Cancelled, (await receiptCancelResponse.ReadAsAsync<InventoryReceiptResponseItem>(HttpStatusCode.OK, options)).Status);
        Assert.Equal(PurchaseOrderStatus.Cancelled, (await client.GetJsonAsync<PurchaseOrderResponseItem>($"{ApiRoutes.PurchaseOrders}/{second.Id}", options)).Status);

        // Act / Assert: 下書きはキャンセルでき、キャンセルした発注は発注できない。ない発注は 404
        var third = await CreateDraftAsync(client);
        using var draftCancelResponse = await client.PostAsync(new Uri($"{ApiRoutes.PurchaseOrders}/{third.Id}/cancel", UriKind.Relative), null, Token);
        Assert.Equal(PurchaseOrderStatus.Cancelled, (await draftCancelResponse.ReadAsAsync<PurchaseOrderResponseItem>(HttpStatusCode.OK, options)).Status);
        using var orderCancelledResponse = await client.PostAsync(new Uri($"{ApiRoutes.PurchaseOrders}/{third.Id}/order", UriKind.Relative), null, Token);
        await orderCancelledResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "PURCHASE_ORDER_STATUS_INVALID", options);
        using var missingResponse = await client.PostAsync(new Uri($"{ApiRoutes.PurchaseOrders}/{Guid.NewGuid()}/cancel", UriKind.Relative), null, Token);
        await missingResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);

        // Act / Assert: 未完了の一覧には下書きと発注済みだけが出る
        var open = await client.GetJsonAsync<PurchaseOrderResponse>($"{ApiRoutes.PurchaseOrders}?open=true&size={ApiDefaults.MaxPageSize}", options);
        Assert.DoesNotContain(open.Items, x => (x.Id == first.Id) || (x.Id == second.Id) || (x.Id == third.Id));
        Assert.All(open.Items, static x => Assert.True(x.Status.IsOpen()));
    }

    // 登録の検証 (仕入先・商品がない、明細がない)、発注書 PDF、端末からは使えない (発注は管理画面だけ)
    [Fact]
    public async Task CreateValidatesAndPrintsAndRejectsTerminal()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var draft = await CreateDraftAsync(client);
        var line = new PurchaseOrderCreateRequestLine { ProductId = TestData.SdCardProductId, Quantity = 1m };

        // Act / Assert: 仕入先がなければ 422 VALIDATION_ERROR、商品がなければ 422 PRODUCT_NOT_FOUND、明細がなければ 400
        using var supplierResponse = await client.PostJsonAsync(ApiRoutes.PurchaseOrders, new PurchaseOrderCreateRequest { StoreId = TestData.MainStoreId, SupplierId = Guid.NewGuid(), Lines = [line] }, options);
        await supplierResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        using var productResponse = await client.PostJsonAsync(ApiRoutes.PurchaseOrders, new PurchaseOrderCreateRequest { StoreId = TestData.MainStoreId, SupplierId = TestData.SupplierId, Lines = [new PurchaseOrderCreateRequestLine { ProductId = Guid.NewGuid(), Quantity = 1m }] }, options);
        await productResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "PRODUCT_NOT_FOUND", options);
        using var emptyResponse = await client.PostJsonAsync(ApiRoutes.PurchaseOrders, new PurchaseOrderCreateRequest { StoreId = TestData.MainStoreId, SupplierId = TestData.SupplierId, Lines = [] }, options);
        await emptyResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);

        // Act / Assert: 発注書 PDF
        using var pdfResponse = await client.GetAsync(new Uri($"{ApiRoutes.PurchaseOrders}/{draft.Id}/pdf", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType?.MediaType);
        var bytes = await pdfResponse.Content.ReadAsByteArrayAsync(Token);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Directory.CreateDirectory("TestResults");
        await File.WriteAllBytesAsync(Path.Combine("TestResults", "purchase-order.pdf"), bytes, Token);

        // Act / Assert: 端末のトークンでは使えない (管理画面だけ)
        var terminal = await factory.CreateTerminalClientAsync(TestData.MainTerminal1Id);
        using var terminalResponse = await terminal.GetAsync(new Uri(ApiRoutes.PurchaseOrders, UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Forbidden, terminalResponse.StatusCode);
    }

    private async Task<PurchaseOrderResponseItem> CreateDraftAsync(HttpClient client)
    {
        var request = new PurchaseOrderCreateRequest
        {
            StoreId = TestData.MainStoreId,
            SupplierId = TestData.SupplierId,
            Lines = [new PurchaseOrderCreateRequestLine { ProductId = TestData.SdCardProductId, Quantity = 3m, Cost = 1200m }]
        };
        using var response = await client.PostJsonAsync(ApiRoutes.PurchaseOrders, request, options);
        return await response.ReadAsAsync<PurchaseOrderResponseItem>(HttpStatusCode.Created, options);
    }

    private async Task<PurchaseOrderResponseItem> OrderAsync(HttpClient client, PurchaseOrderResponseItem draft)
    {
        using var response = await client.PostAsync(new Uri($"{ApiRoutes.PurchaseOrders}/{draft.Id}/order", UriKind.Relative), null, Token);
        return await response.ReadAsAsync<PurchaseOrderResponseItem>(HttpStatusCode.OK, options);
    }

    private async Task<decimal> QuantityAsync(HttpClient client, Guid storeId, Guid productId)
    {
        var levels = await client.GetJsonAsync<InventoryProductResponse>($"{ApiRoutes.Inventory}/{productId}", options);
        return levels.Levels.FirstOrDefault(x => x.StoreId == storeId)?.Quantity ?? 0m;
    }
}
