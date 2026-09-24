namespace Pos.Domain.Enums;

// 受注の種別。取り寄せは入荷を待ち、取り置きは店頭の在庫を確保する
public enum OrderType
{
    BackOrder,
    Hold
}
