namespace Pos.Server.Services;

using System.Security.Cryptography;

using Pos.Domain;
using Pos.Server.Accessors;
using Pos.Server.Infrastructure.Imaging;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

// 商品画像 (Version は URL の v に使う内容のハッシュ)
public sealed record ProductImageResult(ReadOnlyMemory<byte> Data, string ContentType, string Version);

// 商品 CSV の取込の誤り (列と種類。文言は Host が付ける)
public sealed record ProductImportError(ProductImportColumn Column, ProductImportProblem Problem);

public sealed record ProductImportLineResult(int LineNo, string? Code, string? Name, ImportAction Action, IReadOnlyList<ProductImportError> Errors);

// Status: Success (反映した、または dryRun で誤りなし) / Invalid (誤りのある行がある) / VersionMismatch (反映中に他で更新された)
public sealed record ProductImportResult(DataWriteStatus Status, IReadOnlyList<ProductImportLineResult> Lines)
{
    public int InsertCount => Lines.Count(static x => x.Action == ImportAction.Insert);

    public int UpdateCount => Lines.Count(static x => x.Action == ImportAction.Update);

    public int UnchangedCount => Lines.Count(static x => x.Action == ImportAction.Unchanged);

    public int ErrorCount => Lines.Count(static x => x.Action == ImportAction.Error);
}

public sealed class ProductService
{
    // 画像は JPEG / PNG の 2 MB まで (縮小はしない)
    public const int ImageMaxBytes = 2 * 1024 * 1024;

    private readonly TimeProvider timeProvider;
    private readonly IDbProvider provider;
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;
    private readonly ProductAccessor productAccessor;

    public ProductService(
        TimeProvider timeProvider,
        IDbProvider provider,
        IDialect dialect,
        MasterAccessor masterAccessor,
        ProductAccessor productAccessor)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
        this.productAccessor = productAccessor;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    // 差分同期 (UpdatedSince 指定時) は updatedAt, id 順 (SQL 側で固定)
    public async ValueTask<PagedResult<ProductEntity>> QueryPageAsync(ProductQueryParameter parameter, CancellationToken cancellationToken)
    {
        var keyword = ServiceHelper.ToLikePattern(dialect, parameter.Keyword);
        var total = await productAccessor.CountAsync(parameter.CategoryId, keyword, parameter.IsActive, parameter.UpdatedSince, parameter.IncludeDeleted, cancellationToken);
        var items = await productAccessor.QueryListAsync(parameter.CategoryId, keyword, parameter.IsActive, parameter.UpdatedSince, parameter.IncludeDeleted, parameter.Sort, parameter.Desc, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        return new PagedResult<ProductEntity>((int)total, parameter.Page, parameter.Size, items);
    }

    // 全件 (コード順)
    public ValueTask<List<ProductEntity>> QueryAllAsync(bool includeDeleted, CancellationToken cancellationToken) =>
        productAccessor.QueryAllAsync(includeDeleted, cancellationToken);

    // CSV 出力 (削除済みを除く全件、コード順)
    public ValueTask<List<ProductExportView>> QueryExportListAsync(CancellationToken cancellationToken) =>
        productAccessor.QueryExportListAsync(cancellationToken);

    public ValueTask<ProductEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        productAccessor.QueryAsync(id, cancellationToken);

    public ValueTask<ProductEntity?> QueryByBarcodeAsync(string barcode, CancellationToken cancellationToken) =>
        productAccessor.QueryByBarcodeAsync(barcode, cancellationToken);

    public ValueTask<ProductEntity?> QueryByCodeAsync(string code, CancellationToken cancellationToken) =>
        productAccessor.QueryByCodeAsync(code, cancellationToken);

    //--------------------------------------------------------------------------------
    // Write
    //--------------------------------------------------------------------------------

    public ValueTask<DataWriteStatus> InsertAsync(ProductEntity entity, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        return ServiceHelper.InsertAsync(dialect, () => WriteAsync(tx => productAccessor.InsertAsync(tx, entity, cancellationToken), cancellationToken));
    }

    public ValueTask<DataWriteResult<ProductEntity>> UpdateAsync(ProductEntity entity, CancellationToken cancellationToken)
    {
        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        return ServiceHelper.UpdateAsync(
            dialect,
            () => WriteAsync(tx => UpdateAsync(tx, entity, cancellationToken), cancellationToken),
            async () => await productAccessor.QueryAsync(entity.Id, cancellationToken) is { IsDeleted: false });
    }

    public async ValueTask<DataWriteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await productAccessor.DeleteAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken) > 0 ? DataWriteStatus.Success : DataWriteStatus.NotFound;

    // 登録と更新の Accessor は取込と共用でトランザクションを受けるので、1 件の書き込みもトランザクションの中で行う
    private ValueTask<T> WriteAsync<T>(Func<DbTransaction, ValueTask<T>> write, CancellationToken cancellationToken) =>
        provider.UsingTxAsync(async (_, tx) =>
        {
            var result = await write(tx);
            await tx.CommitAsync(cancellationToken);
            return result;
        }, cancellationToken);

    private ValueTask<ProductEntity?> UpdateAsync(DbTransaction tx, ProductEntity entity, CancellationToken cancellationToken) =>
        productAccessor.UpdateAsync(
            tx,
            entity.Id,
            entity.Code,
            entity.Barcode,
            entity.Name,
            entity.Kana,
            entity.Brand,
            entity.ModelNo,
            entity.CategoryId,
            entity.Kind,
            entity.Price,
            entity.TaxIncluded,
            entity.TaxRateId,
            entity.Cost,
            entity.PointRate,
            entity.RequiresSerial,
            entity.TrackInventory,
            entity.AllowsPriceOverride,
            entity.Unit,
            entity.IsActive,
            entity.UpdatedAt,
            entity.Version,
            cancellationToken);

    //--------------------------------------------------------------------------------
    // Image
    //--------------------------------------------------------------------------------

    public async ValueTask<ProductImageResult?> QueryImageAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await productAccessor.QueryImageDataAsync(id, cancellationToken) is not byte[] data)
        {
            return null;
        }

        return new ProductImageResult(data, ImageContentType.Detect(data) ?? "application/octet-stream", ImageVersion(data));
    }

    // 画像を置き換え、商品の ImageUrl を {imagePath}?v={内容のハッシュ} にする。
    // 商品の UpdatedAt / Version も進むので、端末は差分同期で新しい URL を受け取り、v が変わった画像だけを取り直す
    public async ValueTask<DataWriteResult<ProductEntity>> SaveImageAsync(Guid id, ReadOnlyMemory<byte> data, string imagePath, CancellationToken cancellationToken)
    {
        if ((data.Length == 0) || (data.Length > ImageMaxBytes) || (ImageContentType.Detect(data.Span) is null))
        {
            return new DataWriteResult<ProductEntity>(DataWriteStatus.Invalid, null);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var imageUrl = $"{imagePath}?v={ImageVersion(data.Span)}";
        var entity = await provider.UsingTxAsync(async (_, tx) =>
        {
            var updated = await productAccessor.UpdateImageUrlAsync(tx, id, imageUrl, now, cancellationToken);
            if (updated is null)
            {
                return null;
            }

            await productAccessor.UpsertImageAsync(tx, id, data.ToArray(), now, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return updated;
        }, cancellationToken);
        return new DataWriteResult<ProductEntity>(entity is null ? DataWriteStatus.NotFound : DataWriteStatus.Success, entity);
    }

    // 画像がなければ何もしない (商品の版を進めない)
    public async ValueTask<DataWriteResult<ProductEntity>> DeleteImageAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await productAccessor.QueryAsync(id, cancellationToken);
        if ((product is null) || product.IsDeleted)
        {
            return new DataWriteResult<ProductEntity>(DataWriteStatus.NotFound, null);
        }

        if (product.ImageUrl is null)
        {
            return new DataWriteResult<ProductEntity>(DataWriteStatus.Success, product);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = await provider.UsingTxAsync(async (_, tx) =>
        {
            var updated = await productAccessor.UpdateImageUrlAsync(tx, id, null, now, cancellationToken);
            if (updated is null)
            {
                return null;
            }

            await productAccessor.DeleteImageAsync(tx, id, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return updated;
        }, cancellationToken);
        return new DataWriteResult<ProductEntity>(entity is null ? DataWriteStatus.NotFound : DataWriteStatus.Success, entity);
    }

    // 内容が同じなら同じ値になる (同じ画像の再登録で端末に取り直させない)
    private static string ImageVersion(ReadOnlySpan<byte> data) =>
        Convert.ToHexStringLower(SHA256.HashData(data))[..16];

    //--------------------------------------------------------------------------------
    // Import
    //--------------------------------------------------------------------------------

    // CSV の取込。全行を検証し、誤りが 1 行でもあれば何も反映しない (dryRun は検証だけ)。
    // コードが一致すれば更新、なければ登録し、変更のない行は書き込まない。CSV にない商品は削除しない
    public async ValueTask<ProductImportResult> ImportAsync(IReadOnlyList<ProductImportLine> lines, bool dryRun, CancellationToken cancellationToken)
    {
        var categories = (await masterAccessor.QueryCategoryAllAsync(false, cancellationToken)).ToDictionary(static x => x.Code, StringComparer.Ordinal);
        var taxRates = (await masterAccessor.QueryTaxRateListAsync(null, false, cancellationToken)).ToDictionary(static x => x.Code, StringComparer.Ordinal);
        var products = await productAccessor.QueryAllAsync(true, cancellationToken);
        var byCode = products.ToDictionary(static x => x.Code, StringComparer.Ordinal);
        var byBarcode = products.Where(static x => x.Barcode is not null).ToDictionary(static x => x.Barcode!, StringComparer.Ordinal);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var codes = new HashSet<string>(StringComparer.Ordinal);
        var barcodes = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<ProductImportLineResult>(lines.Count);
        var inserts = new List<ProductEntity>();
        var updates = new List<ProductEntity>();
        foreach (var line in lines)
        {
            var errors = new List<ProductImportError>();
            var entity = ToEntity(line, categories, taxRates, errors);

            // コードと JAN はファイルの中で一意で、他の商品 (削除済みを含む) と重ならないこと
            var existing = entity.Code.Length > 0 ? byCode.GetValueOrDefault(entity.Code) : null;
            if ((entity.Code.Length > 0) && !codes.Add(entity.Code))
            {
                errors.Add(new ProductImportError(ProductImportColumn.Code, ProductImportProblem.Duplicated));
            }
            else if (existing is { IsDeleted: true })
            {
                errors.Add(new ProductImportError(ProductImportColumn.Code, ProductImportProblem.InUse));
            }

            if (entity.Barcode is not null)
            {
                if (!barcodes.Add(entity.Barcode))
                {
                    errors.Add(new ProductImportError(ProductImportColumn.Barcode, ProductImportProblem.Duplicated));
                }
                else if (byBarcode.TryGetValue(entity.Barcode, out var other) && (other.Code != entity.Code))
                {
                    errors.Add(new ProductImportError(ProductImportColumn.Barcode, ProductImportProblem.InUse));
                }
            }

            ImportAction action;
            if (errors.Count > 0)
            {
                action = ImportAction.Error;
            }
            else if (existing is null)
            {
                entity.Id = Guid.CreateVersion7();
                entity.CreatedAt = now;
                entity.UpdatedAt = now;
                entity.Version = 1;
                inserts.Add(entity);
                action = ImportAction.Insert;
            }
            else if (IsSame(entity, existing))
            {
                action = ImportAction.Unchanged;
            }
            else
            {
                entity.Id = existing.Id;
                entity.UpdatedAt = now;
                entity.Version = existing.Version;
                updates.Add(entity);
                action = ImportAction.Update;
            }

            results.Add(new ProductImportLineResult(line.LineNo, Normalize(line.Code), Normalize(line.Name), action, errors));
        }

        if (results.Any(static x => x.Action == ImportAction.Error))
        {
            return new ProductImportResult(DataWriteStatus.Invalid, results);
        }

        if (dryRun || ((inserts.Count == 0) && (updates.Count == 0)))
        {
            return new ProductImportResult(DataWriteStatus.Success, results);
        }

        // 検証の後に他で変わっていたら (一意制約・外部キーの違反、版の不一致) 全体を取り消す
        try
        {
            await provider.UsingTxAsync(async (_, tx) =>
            {
                foreach (var entity in inserts)
                {
                    await productAccessor.InsertAsync(tx, entity, cancellationToken);
                }

                foreach (var entity in updates)
                {
                    if (await UpdateAsync(tx, entity, cancellationToken) is null)
                    {
                        throw new DBConcurrencyException();
                    }
                }

                await tx.CommitAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return new ProductImportResult(DataWriteStatus.VersionMismatch, results);
        }
        catch (DBConcurrencyException)
        {
            return new ProductImportResult(DataWriteStatus.VersionMismatch, results);
        }

        return new ProductImportResult(DataWriteStatus.Success, results);
    }

    // 文字列の値を変換して検証する (誤りは errors に足し、エンティティには既定値を入れる)
    private static ProductEntity ToEntity(ProductImportLine line, Dictionary<string, CategoryEntity> categories, Dictionary<string, TaxRateEntity> taxRates, List<ProductImportError> errors)
    {
        var entity = new ProductEntity
        {
            Code = RequiredText(line.Code, Length.Code, ProductImportColumn.Code, errors) ?? string.Empty,
            Barcode = OptionalText(line.Barcode, Length.Barcode, ProductImportColumn.Barcode, errors),
            Name = RequiredText(line.Name, Length.Name, ProductImportColumn.Name, errors) ?? string.Empty,
            Kana = OptionalText(line.Kana, Length.Kana, ProductImportColumn.Kana, errors),
            Brand = OptionalText(line.Brand, Length.Brand, ProductImportColumn.Brand, errors),
            ModelNo = OptionalText(line.ModelNo, Length.ModelNo, ProductImportColumn.ModelNo, errors),
            Unit = OptionalText(line.Unit, Length.Unit, ProductImportColumn.Unit, errors),
            Price = Amount(line.Price, true, ProductImportColumn.Price, errors) ?? 0m,
            Cost = Amount(line.Cost, false, ProductImportColumn.Cost, errors),
            TaxIncluded = Flag(line.TaxIncluded, ProductImportColumn.TaxIncluded, errors),
            RequiresSerial = Flag(line.RequiresSerial, ProductImportColumn.RequiresSerial, errors),
            TrackInventory = Flag(line.TrackInventory, ProductImportColumn.TrackInventory, errors),
            AllowsPriceOverride = Flag(line.AllowsPriceOverride, ProductImportColumn.AllowsPriceOverride, errors),
            IsActive = Flag(line.IsActive, ProductImportColumn.IsActive, errors)
        };

        var categoryCode = RequiredText(line.CategoryCode, Length.Code, ProductImportColumn.CategoryCode, errors);
        if (categoryCode is not null)
        {
            if (categories.TryGetValue(categoryCode, out var category))
            {
                entity.CategoryId = category.Id;
            }
            else
            {
                errors.Add(new ProductImportError(ProductImportColumn.CategoryCode, ProductImportProblem.NotFound));
            }
        }

        var taxRateCode = RequiredText(line.TaxRateCode, Length.TaxRateCode, ProductImportColumn.TaxRateCode, errors);
        if (taxRateCode is not null)
        {
            if (taxRates.TryGetValue(taxRateCode, out var taxRate))
            {
                entity.TaxRateId = taxRate.Id;
            }
            else
            {
                errors.Add(new ProductImportError(ProductImportColumn.TaxRateCode, ProductImportProblem.NotFound));
            }
        }

        // 種別は列挙名 (CSV 出力と同じ)。数値は受け付けない
        var kind = Normalize(line.Kind);
        if (kind is null)
        {
            errors.Add(new ProductImportError(ProductImportColumn.Kind, ProductImportProblem.Required));
        }
        else if (!Char.IsLetter(kind[0]) || !Enum.TryParse<ProductKind>(kind, true, out var value))
        {
            errors.Add(new ProductImportError(ProductImportColumn.Kind, ProductImportProblem.Invalid));
        }
        else
        {
            entity.Kind = value;
        }

        // 還元率は 0〜1 (0.10 = 10%)
        var pointRate = Amount(line.PointRate, true, ProductImportColumn.PointRate, errors);
        if (pointRate > 1m)
        {
            errors.Add(new ProductImportError(ProductImportColumn.PointRate, ProductImportProblem.Invalid));
        }
        else
        {
            entity.PointRate = pointRate ?? 0m;
        }

        return entity;
    }

    private static string? Normalize(string? value) =>
        String.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? OptionalText(string? value, int maxLength, ProductImportColumn column, List<ProductImportError> errors)
    {
        var text = Normalize(value);
        if ((text is not null) && (text.Length > maxLength))
        {
            errors.Add(new ProductImportError(column, ProductImportProblem.TooLong));
        }

        return text;
    }

    private static string? RequiredText(string? value, int maxLength, ProductImportColumn column, List<ProductImportError> errors)
    {
        var text = OptionalText(value, maxLength, column, errors);
        if (text is null)
        {
            errors.Add(new ProductImportError(column, ProductImportProblem.Required));
        }

        return text;
    }

    // 金額・率は 0 以上 (桁区切りは Excel で編集したときのために受け付ける)
    private static decimal? Amount(string? value, bool required, ProductImportColumn column, List<ProductImportError> errors)
    {
        var text = Normalize(value);
        if (text is null)
        {
            if (required)
            {
                errors.Add(new ProductImportError(column, ProductImportProblem.Required));
            }

            return null;
        }

        if (!Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || (amount < 0m))
        {
            errors.Add(new ProductImportError(column, ProductImportProblem.Invalid));
            return null;
        }

        return amount;
    }

    // True / False (CSV 出力と同じ) と 1 / 0
    private static bool Flag(string? value, ProductImportColumn column, List<ProductImportError> errors)
    {
        var text = Normalize(value);
        if (text is null)
        {
            errors.Add(new ProductImportError(column, ProductImportProblem.Required));
            return false;
        }

        if (Boolean.TryParse(text, out var flag))
        {
            return flag;
        }

        if (text is "1" or "0")
        {
            return text == "1";
        }

        errors.Add(new ProductImportError(column, ProductImportProblem.Invalid));
        return false;
    }

    private static bool IsSame(ProductEntity x, ProductEntity y) =>
        (x.Code == y.Code) &&
        (x.Barcode == y.Barcode) &&
        (x.Name == y.Name) &&
        (x.Kana == y.Kana) &&
        (x.Brand == y.Brand) &&
        (x.ModelNo == y.ModelNo) &&
        (x.CategoryId == y.CategoryId) &&
        (x.Kind == y.Kind) &&
        (x.Price == y.Price) &&
        (x.TaxIncluded == y.TaxIncluded) &&
        (x.TaxRateId == y.TaxRateId) &&
        (x.Cost == y.Cost) &&
        (x.PointRate == y.PointRate) &&
        (x.RequiresSerial == y.RequiresSerial) &&
        (x.TrackInventory == y.TrackInventory) &&
        (x.AllowsPriceOverride == y.AllowsPriceOverride) &&
        (x.Unit == y.Unit) &&
        (x.IsActive == y.IsActive);
}
