namespace Pos.Terminal.Models.Customer;

using Pos.Contract.Customers;

// 会員の検索結果の行 (会員選択と会員照会。アバターは名前の頭文字と会員番号で決まる色)
public sealed record CustomerItem(CustomerListResponseItem Customer, string Name, string Code, string Phone, string PointsText);
