namespace Pos.Domain.Logic;

// 担当・承認者の業務ルール: 有効で、その店舗 (または本部) に所属していること。承認できるのは店長以上
public static class StaffLogic
{
    public static bool CanApprove(StaffRole role) =>
        role is StaffRole.Manager or StaffRole.Admin;

    // レジ係の取消は店長以上の承認が要る
    public static bool RequiresVoidApproval(StaffRole role) =>
        role == StaffRole.Cashier;

    public static bool IsAllowed(StaffFact? staff, Guid storeId) =>
        (staff is { IsActive: true }) && ((staff.StoreId is null) || (staff.StoreId == storeId));

    public static RuleError? ValidateStaff(StaffFact? staff, Guid storeId) =>
        IsAllowed(staff, storeId) ? null : new RuleError(ErrorCode.StaffInvalid, RuleReason.StaffNotAllowed);

    // missing は承認者の指定がないときの理由
    public static RuleError? ValidateApprover(Guid? approverId, StaffFact? approver, Guid storeId, RuleReason missing, Guid? lineId = null)
    {
        if (approverId is null)
        {
            return new RuleError(ErrorCode.ApprovalRequired, missing, lineId);
        }

        return IsAllowed(approver, storeId) && CanApprove(approver!.Role) ? null : new RuleError(ErrorCode.ApprovalRequired, RuleReason.ApproverNotAllowed, lineId);
    }
}
