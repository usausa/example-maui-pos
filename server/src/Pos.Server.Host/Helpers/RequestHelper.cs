namespace Pos.Server.Host.Helpers;

using Microsoft.Net.Http.Headers;

// 本文をそのまま受ける API (画像・CSV) の読み取り
public static class RequestHelper
{
    // Content-Type のメディアタイプだけを比べる (charset などのパラメータは見ない)
    public static bool IsMediaType(HttpRequest request, params ReadOnlySpan<string> mediaTypes)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var value))
        {
            return false;
        }

        foreach (var mediaType in mediaTypes)
        {
            if (value.MediaType.Equals(mediaType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // 上限を超えたら null。Content-Length がない要求もあるので読みながら確かめる
    public static async ValueTask<byte[]?> ReadBodyAsync(HttpRequest request, int maxBytes, CancellationToken cancellationToken)
    {
        if (request.ContentLength > maxBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }
}
