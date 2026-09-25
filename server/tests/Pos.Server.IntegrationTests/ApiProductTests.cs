namespace Pos.Server;

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using CsvHelper;

using Pos.Contract.Products;
using Pos.Contract.Sync;
using Pos.Server.Host.Endpoints;
using Pos.Server.Host.Models.Export;
using Pos.Server.Services;

// 商品画像 (登録 → 取得・再検証 → 差分同期 → 削除) と CSV の取込 (プレビュー → 反映 → 変更なし → 誤りは全体を反映しない)
public sealed class ApiProductTests : IClassFixture<TestApplicationFactory>
{
    // 1×1 の PNG
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private readonly TestApplicationFactory factory;

    private readonly JsonSerializerOptions options;

    public ApiProductTests(TestApplicationFactory factory)
    {
        this.factory = factory;
        options = factory.JsonOptions();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // 画像は内容のハッシュ付きの URL で配り、商品の版を進めて差分同期に載せる
    [Fact]
    public async Task ImageIsServedWithVersionedUrlAndSynced()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var imageUrl = ApiRoutes.ProductImage(TestData.CameraProductId);
        var original = await client.GetJsonAsync<ProductResponseItem>($"{ApiRoutes.Products}/{TestData.CameraProductId}", options);
        var sync = await client.GetJsonAsync<SyncMastersResponse>($"{ApiRoutes.Sync}/masters", options);

        // Act / Assert: 登録すると ImageUrl に v が付き、版が進む
        using var putResponse = await PutImageAsync(client, imageUrl, Png, "image/png");
        var product = await putResponse.ReadAsAsync<ProductResponseItem>(HttpStatusCode.OK, options);
        Assert.StartsWith($"{imageUrl}?v=", product.ImageUrl, StringComparison.Ordinal);
        Assert.Equal(original.Version + 1, product.Version);

        // Act / Assert: 取得は同じ本文。v が一致すれば長くキャッシュでき、ETag で再検証すると 304
        using var getResponse = await client.GetAsync(new Uri(product.ImageUrl!, UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("image/png", getResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await getResponse.Content.ReadAsByteArrayAsync(Token));
        Assert.True(getResponse.Headers.CacheControl?.Private);
        Assert.NotNull(getResponse.Headers.ETag);
        using var revalidate = new HttpRequestMessage(HttpMethod.Get, new Uri(product.ImageUrl!, UriKind.Relative));
        revalidate.Headers.IfNoneMatch.Add(getResponse.Headers.ETag);
        using var notModified = await client.SendAsync(revalidate, Token);
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);

        // Act / Assert: 同じ画像の再登録は同じ URL (端末に取り直させない)。差分同期で URL が届く
        using var samePutResponse = await PutImageAsync(client, imageUrl, Png, "image/png");
        Assert.Equal(product.ImageUrl, (await samePutResponse.ReadAsAsync<ProductResponseItem>(HttpStatusCode.OK, options)).ImageUrl);
        var delta = await client.GetJsonAsync<SyncMastersResponse>($"{ApiRoutes.Sync}/masters?since={sync.ServerTime:yyyy-MM-ddTHH:mm:ss.fffZ}", options);
        Assert.Equal(product.ImageUrl, delta.Products.Single(static x => x.Id == TestData.CameraProductId).ImageUrl);

        // Act / Assert: 商品の更新は画像に触れない
        var current = await client.GetJsonAsync<ProductResponseItem>($"{ApiRoutes.Products}/{TestData.CameraProductId}", options);
        using var updateResponse = await client.PutJsonAsync($"{ApiRoutes.Products}/{TestData.CameraProductId}", ToUpdateRequest(current), options);
        Assert.Equal(product.ImageUrl, (await updateResponse.ReadAsAsync<ProductResponseItem>(HttpStatusCode.OK, options)).ImageUrl);

        // Act / Assert: 画像でない本文は 422、JPEG / PNG 以外の Content-Type は 415、2 MB を超えると 413、商品がなければ 404
        using var invalidResponse = await PutImageAsync(client, imageUrl, "GIF89a"u8.ToArray(), "image/png");
        await invalidResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        using var unsupportedResponse = await PutImageAsync(client, imageUrl, Png, "image/gif");
        await unsupportedResponse.ReadProblemAsync(HttpStatusCode.UnsupportedMediaType, "VALIDATION_ERROR", options);
        var large = new byte[ProductService.ImageMaxBytes + 1];
        Png.CopyTo(large, 0);
        using var largeResponse = await PutImageAsync(client, imageUrl, large, "image/png");
        await largeResponse.ReadProblemAsync(HttpStatusCode.RequestEntityTooLarge, "VALIDATION_ERROR", options);
        using var missingResponse = await PutImageAsync(client, ApiRoutes.ProductImage(Guid.NewGuid()), Png, "image/png");
        await missingResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);

        // Act / Assert: 削除すると ImageUrl が消えて版が進み、画像は 404
        using var deleteResponse = await client.DeleteUrlAsync(imageUrl);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var deleted = await client.GetJsonAsync<ProductResponseItem>($"{ApiRoutes.Products}/{TestData.CameraProductId}", options);
        Assert.Null(deleted.ImageUrl);
        Assert.True(deleted.Version > current.Version);
        using var goneResponse = await client.GetAsync(new Uri(imageUrl, UriKind.Relative), Token);
        await goneResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);
    }

    // 出力した CSV を編集して戻す。プレビューは反映せず、誤りが 1 行でもあれば何も反映しない
    [Fact]
    public async Task ImportPreviewsThenAppliesAllOrNothing()
    {
        // Arrange: SD カードの価格を変え、新しい商品を足す
        var client = await factory.CreateAdminClientAsync();
        using var exportResponse = await client.GetAsync(new Uri($"{ApiRoutes.Products}/csv", UriKind.Relative), Token);
        var rows = FromCsv(await exportResponse.Content.ReadAsStringAsync(Token));
        var sdCard = await client.GetJsonAsync<ProductResponseItem>($"{ApiRoutes.Products}/{TestData.SdCardProductId}", options);
        var sdCardRow = rows.Single(x => x.Code == sdCard.Code);
        sdCardRow.Price += 100m;
        var code = NewCode();
        rows.Add(new ProductExportRow
        {
            Code = code,
            Name = "取込テスト商品",
            CategoryCode = sdCardRow.CategoryCode,
            CategoryName = sdCardRow.CategoryName,
            Kind = ProductKind.Goods,
            Price = 1500m,
            TaxIncluded = true,
            TaxRateCode = sdCardRow.TaxRateCode,
            PointRate = 0.01m,
            TrackInventory = true,
            IsActive = true
        });
        var body = ToCsv(rows);

        // Act / Assert: プレビューは行ごとの結果だけで、何も変えない
        using var previewResponse = await PostCsvAsync(client, Encoding.UTF8.GetBytes(body), true);
        var preview = await previewResponse.ReadAsAsync<ProductImportResponse>(HttpStatusCode.OK, options);
        Assert.True(preview.DryRun);
        Assert.Equal((1, 1, rows.Count - 2, 0), (preview.InsertCount, preview.UpdateCount, preview.UnchangedCount, preview.ErrorCount));
        Assert.Equal(ImportAction.Update, preview.Items.Single(x => x.Code == sdCard.Code).Action);
        var inserted = preview.Items.Single(x => x.Code == code);
        Assert.Equal((ImportAction.Insert, rows.Count + 1), (inserted.Action, inserted.LineNo));
        Assert.Equal(sdCard.Price, (await client.GetJsonAsync<ProductResponseItem>($"{ApiRoutes.Products}/{TestData.SdCardProductId}", options)).Price);

        // Act / Assert: 反映すると登録と更新が入る。同じ CSV をもう一度送るとすべて変更なし
        using var importResponse = await PostCsvAsync(client, Encoding.UTF8.GetBytes(body), false);
        var imported = await importResponse.ReadAsAsync<ProductImportResponse>(HttpStatusCode.OK, options);
        Assert.Equal((false, 1, 1), (imported.DryRun, imported.InsertCount, imported.UpdateCount));
        var updated = await client.GetJsonAsync<ProductResponseItem>($"{ApiRoutes.Products}/{TestData.SdCardProductId}", options);
        Assert.Equal((sdCard.Price + 100m, sdCard.Version + 1), (updated.Price, updated.Version));
        var created = await client.GetJsonAsync<ProductResponseItem>($"{ApiRoutes.Products}/lookup?code={code}", options);
        Assert.Equal((sdCard.CategoryId, 1500m), (created.CategoryId, created.Price));
        using var againResponse = await PostCsvAsync(client, Encoding.UTF8.GetBytes(body), false);
        Assert.Equal(rows.Count, (await againResponse.ReadAsAsync<ProductImportResponse>(HttpStatusCode.OK, options)).UnchangedCount);

        // Act / Assert: Excel の既定の Shift_JIS も読める
        using var sjisResponse = await PostCsvAsync(client, CodePagesEncodingProvider.Instance.GetEncoding(932)!.GetBytes(body), true);
        Assert.Equal(rows.Count, (await sjisResponse.ReadAsAsync<ProductImportResponse>(HttpStatusCode.OK, options)).UnchangedCount);

        // Act / Assert: 部門コードの誤り・数値でない価格・ファイルの中のコードの重複があれば 422 で、正しい行 (2 行目) も反映しない
        var header = body[..body.IndexOf('\r', StringComparison.Ordinal)];
        var duplicated = NewCode();
        var invalid = String.Join(
            "\r\n",
            header,
            $"{sdCard.Code},{sdCard.Barcode},{sdCard.Name},,,,{sdCardRow.CategoryCode},,Goods,1,True,{sdCardRow.TaxRateCode},,0,False,True,False,,True",
            $"{NewCode()},,商品 2,,,,NOPE,,Goods,100,True,{sdCardRow.TaxRateCode},,0,False,True,False,,True",
            $"{NewCode()},,商品 3,,,,{sdCardRow.CategoryCode},,Goods,abc,True,{sdCardRow.TaxRateCode},,0,False,True,False,,True",
            $"{duplicated},,商品 4,,,,{sdCardRow.CategoryCode},,Goods,100,True,{sdCardRow.TaxRateCode},,0,False,True,False,,True",
            $"{duplicated},,商品 4,,,,{sdCardRow.CategoryCode},,Goods,100,True,{sdCardRow.TaxRateCode},,0,False,True,False,,True");
        using var invalidResponse = await PostCsvAsync(client, Encoding.UTF8.GetBytes(invalid), false);
        var problem = await invalidResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        Assert.Equal(["3", "4", "6"], problem.Errors!.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("「部門コード」に該当する部門がありません", problem.Errors["3"].Single());
        Assert.Equal("「価格」は 0 以上の数値にしてください", problem.Errors["4"].Single());
        Assert.Equal("「コード」がファイルの中で重複しています", problem.Errors["6"].Single());
        Assert.Equal(updated.Price, (await client.GetJsonAsync<ProductResponseItem>($"{ApiRoutes.Products}/{TestData.SdCardProductId}", options)).Price);
        using var invalidPreviewResponse = await PostCsvAsync(client, Encoding.UTF8.GetBytes(invalid), true);
        Assert.Equal(3, (await invalidPreviewResponse.ReadAsAsync<ProductImportResponse>(HttpStatusCode.OK, options)).ErrorCount);

        // Act / Assert: 列が足りなければ 400、text/csv 以外は 415
        using var missingColumnResponse = await PostCsvAsync(client, "コード,商品名\r\nX,Y"u8.ToArray(), false);
        var missingColumn = await missingColumnResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        Assert.Contains("価格", missingColumn.Title, StringComparison.Ordinal);
        using var jsonResponse = await client.PostJsonAsync($"{ApiRoutes.Products}/import", rows, options);
        await jsonResponse.ReadProblemAsync(HttpStatusCode.UnsupportedMediaType, "VALIDATION_ERROR", options);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // 他のテストと重ならないコード
    private static string NewCode() => $"T{Guid.NewGuid():N}"[..10];

    private static async Task<HttpResponseMessage> PutImageAsync(HttpClient client, string url, byte[] data, string contentType)
    {
        using var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return await client.PutAsync(new Uri(url, UriKind.Relative), content, Token);
    }

    private static async Task<HttpResponseMessage> PostCsvAsync(HttpClient client, byte[] data, bool dryRun)
    {
        using var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return await client.PostAsync(new Uri($"{ApiRoutes.Products}/import?dryRun={dryRun}", UriKind.Relative), content, Token);
    }

    private static List<ProductExportRow> FromCsv(string text)
    {
        using var reader = new StringReader(text);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return csv.GetRecords<ProductExportRow>().ToList();
    }

    private static string ToCsv(IEnumerable<ProductExportRow> rows)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(rows);
        }

        return writer.ToString();
    }

    private static ProductUpdateRequest ToUpdateRequest(ProductResponseItem product) =>
        new()
        {
            Code = product.Code,
            Barcode = product.Barcode,
            Name = product.Name,
            Kana = product.Kana,
            Brand = product.Brand,
            ModelNo = product.ModelNo,
            CategoryId = product.CategoryId,
            Kind = product.Kind,
            Price = product.Price,
            TaxIncluded = product.TaxIncluded,
            TaxRateId = product.TaxRateId,
            Cost = product.Cost,
            PointRate = product.PointRate,
            RequiresSerial = product.RequiresSerial,
            TrackInventory = product.TrackInventory,
            AllowsPriceOverride = product.AllowsPriceOverride,
            Unit = product.Unit,
            IsActive = product.IsActive,
            Version = product.Version
        };
}
