namespace Pos.Terminal.Services;

using Pos.Contract.Transactions;

// 取引からレシートと電子レシート QR の画像を組み立てる。共有するときだけ PNG にする
public sealed class ReceiptService
{
    private readonly DataAccessor accessor;

    private readonly Session session;

    public ReceiptService(
        DataAccessor accessor,
        Session session)
    {
        this.accessor = accessor;
        this.session = session;
    }

    public async ValueTask<SKImage> BuildAsync(TransactionResponseItem transaction)
    {
        // シリアル番号で探した他の店舗・端末の取引は、その店舗と端末で組み立てる
        var store = transaction.StoreId == session.StoreId ? session.Store : await accessor.QueryStoreAsync(transaction.StoreId);
        var terminal = transaction.TerminalId == session.TerminalId ? session.Terminal : await accessor.QueryTerminalAsync(transaction.TerminalId);
        var staff = await accessor.QueryStaffAsync(transaction.StaffId);
        var methods = (await accessor.QueryPaymentMethodListAsync()).ToDictionary(static x => x.Id, static x => x.Name);
        var text = ReceiptTextBuilder.Build(transaction, store, terminal?.Name ?? string.Empty, staff?.Name ?? string.Empty, methods);
        // SkiaSharp の描画は UI スレッドを塞ぐので背景で行う
        return await Task.Run(() => ReceiptImageBuilder.Build(text));
    }

    // QR の生成も初回は UI スレッドを塞ぐので背景で行う
    public static Task<SKImage> BuildQrAsync(string receiptNo) =>
        Task.Run(() => QrImageBuilder.Build(receiptNo));

    public static Task<byte[]> EncodePngAsync(SKImage image) =>
        Task.Run(() =>
        {
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        });
}
