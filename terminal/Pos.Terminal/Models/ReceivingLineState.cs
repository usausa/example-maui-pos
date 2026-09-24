namespace Pos.Terminal.Models;

// 検品の明細の状態 (届いた数と予定・出荷の数の比較)。数えていない明細は受領のときに予定の数で受け取る
public enum ReceivingLineState
{
    Unchecked,
    Match,
    Shortage,
    Excess
}
