namespace Pos.Server.Models.Enums;

// 商品 CSV の取込の誤りの種類。Duplicated はファイルの中の重複、InUse は他の商品 (削除済みを含む) が使っている
public enum ProductImportProblem
{
    Required,
    TooLong,
    Invalid,
    NotFound,
    Duplicated,
    InUse
}
