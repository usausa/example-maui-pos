namespace Pos.Server.Models.Enums;

// 会員一覧の並び順 (列挙名 = 列名)。先頭が既定
public enum CustomerSort
{
    Code,
    Name,
    Kana,
    PointBalance,
    CreatedAt,
    UpdatedAt
}
