namespace Pos.Terminal.Services;

using Pos.Domain.Logic;
using Pos.Terminal.Modules;

// 担当のログインと承認の PIN。同期したハッシュで照合するので、オフラインでも使える
public sealed class PinService
{
    private const int MaxAttempts = 3;

    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly Session session;

    private readonly DataAccessor accessor;

    public PinService(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        Session session,
        DataAccessor accessor)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.session = session;
        this.accessor = accessor;
    }

    // 3 回まで。取り消したとき・3 回違ったときは false
    public async ValueTask<bool> VerifyAsync(StaffResponseItem staff, string title)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var pin = await popupNavigator.InputPinAsync(title);
            if (pin is null)
            {
                return false;
            }

            // PBKDF2 は重いので UI スレッドで計算しない
            if (await Task.Run(() => PinHasher.Verify(pin, staff.PinHash)))
            {
                return true;
            }

            await dialog.InformationAsync(attempt < MaxAttempts ? "PIN が違います。" : $"PIN を {MaxAttempts} 回間違えました。");
        }

        return false;
    }

    // 承認者 (自店か本部の店長以上で、PIN を設定したスタッフ) を選び、その PIN で本人を確かめる。null = 取り消し
    public async ValueTask<StaffResponseItem?> ChooseApproverAsync(string title)
    {
        var staff = session.StoreId is null
            ? []
            : (await accessor.QueryStaffListAsync(session.StoreId.Value)).Where(static x => StaffLogic.CanApprove(x.Role) && (x.PinHash is not null)).ToList();
        if (staff.Count == 0)
        {
            await dialog.InformationAsync("承認できるスタッフ (店長・管理者) がいません。\n管理画面で PIN を設定してください。");
            return null;
        }

        var approver = await popupNavigator.ChooseAsync(staff, static x => $"{x.Name} ({ViewHelper.Name(x.Role)})", title);
        if (approver is null)
        {
            return null;
        }

        return await VerifyAsync(approver, $"{approver.Name} の PIN") ? approver : null;
    }
}
