namespace Pos.Server.Models.Enums;

// 取引一覧の並び順 (列挙名 = 列名)。先頭が既定
public enum TransactionSort
{
    TransactedAt,
    ReceiptNo,
    Total,
    BusinessDate
}
