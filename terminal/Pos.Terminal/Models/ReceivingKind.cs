namespace Pos.Terminal.Models;

// 受領する伝票の種類 (仕入先からの入荷 / 他店からの移動)
public enum ReceivingKind
{
    Receipt,
    Transfer
}
