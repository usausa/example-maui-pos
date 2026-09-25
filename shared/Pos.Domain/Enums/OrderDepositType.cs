namespace Pos.Domain.Enums;

// 受注の前受金。受取 (Receive) と、キャンセルなどで返す返金 (Refund)。会計で充てた分は取引の支払 (Deposit) に残る
public enum OrderDepositType
{
    Receive,
    Refund
}
