namespace Pos.Terminal.Services;

// 商品画像の取得とキャッシュ。ファイル名に URL の v (内容のハッシュ) を含め、v が変わったときだけ取り直す。
// オフラインや取得できないときはキャッシュだけで表示する (画像がなくても販売の操作は止めない)。同期ではまとめて取らない
public sealed class ProductImageService
{
    private readonly ILogger<ProductImageService> log;

    private readonly DeviceState deviceState;

    private readonly HttpService httpService;

    private readonly string directory = Path.Combine(FileSystem.CacheDirectory, "products");

    public ProductImageService(
        ILogger<ProductImageService> log,
        DeviceState deviceState,
        HttpService httpService)
    {
        this.log = log;
        this.deviceState = deviceState;
        this.httpService = httpService;
    }

    // 表示に使うローカルのファイル (画像がない・取得できないときは null)
    public async ValueTask<string?> GetImageFileAsync(ProductResponseItem product, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrEmpty(product.ImageUrl))
        {
            return null;
        }

        var prefix = $"{product.Id:N}_";
        var path = Path.Combine(directory, prefix + VersionOf(product.ImageUrl));
        if (File.Exists(path))
        {
            return path;
        }

        if (!deviceState.NetworkState.IsConnected())
        {
            return null;
        }

        // ImageUrl はサーバの相対パス (/api/v1/products/{id}/image?v=...) なので、接続先の下のパスとして読む
        var result = await httpService.GetBytesAsync(new Uri(product.ImageUrl.TrimStart('/'), UriKind.Relative), cancellationToken);
        if (!result.IsSuccess || (result.Content is null))
        {
            log.DebugProductImageNotLoaded(product.Id, result.Status, (int)result.StatusCode);
            return null;
        }

        // 一時ファイルから置き換え (途中で止まっても壊れた画像を残さない)、古い版を消す
        try
        {
            Directory.CreateDirectory(directory);
            var temp = $"{path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllBytesAsync(temp, result.Content, cancellationToken);
            File.Move(temp, path, true);
            foreach (var old in Directory.EnumerateFiles(directory, prefix + "*"))
            {
                if ((old != path) && !old.EndsWith(".tmp", StringComparison.Ordinal))
                {
                    File.Delete(old);
                }
            }
        }
        catch (IOException ex)
        {
            log.WarnProductImageCache(product.Id, ex);
        }

        return File.Exists(path) ? path : null;
    }

    // v の値 (内容のハッシュ)。ファイル名に使える文字だけを残す
    private static string VersionOf(string imageUrl)
    {
        var index = imageUrl.LastIndexOf("v=", StringComparison.Ordinal);
        var version = index < 0 ? string.Empty : String.Concat(imageUrl[(index + 2)..].Where(Char.IsAsciiLetterOrDigit));
        return version.Length > 0 ? version : "0";
    }
}
