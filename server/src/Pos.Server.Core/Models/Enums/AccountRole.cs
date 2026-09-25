namespace Pos.Server.Models.Enums;

// 管理画面のアカウントの役割。Operator は参照と、取引・在庫・顧客の操作だけ (マスタ・設定・ユーザー・端末登録は不可)
public enum AccountRole
{
    Administrator,
    Operator
}
