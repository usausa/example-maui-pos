namespace Pos.Terminal.Modules.Sales;

using Pos.Terminal.Models.Cart;

public sealed record DiscountParameter(string Title, IReadOnlyList<DiscountResponseItem> Discounts, decimal BaseAmount);

// 値引の選択 (定義済み / 任意額 / 任意率 + 理由)。承認が必要な値引は承認者を選ぶ
public static class DiscountChooser
{
    private const string CustomAmount = "任意額";

    private const string CustomPercent = "任意率";

    public static async ValueTask<CartDiscount?> ChooseAsync(IDialog dialog, DataAccessor accessor, Session session, DiscountParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(parameter);

        var defined = parameter.Discounts.Where(static x => x.IsActive && !x.IsDeleted).OrderBy(static x => x.SortOrder).ToList();
        var items = defined.Select(static x => $"{x.Name} ({Describe(x.Type, x.Value)})").Concat([CustomAmount, CustomPercent]).ToArray();
        var index = await dialog.ChooseAsync(items, parameter.Title);
        if (index < 0)
        {
            return null;
        }

        if (index < defined.Count)
        {
            var definition = defined[index];
            var approver = definition.RequiresApproval ? await ChooseApproverAsync(dialog, accessor, session) : null;
            if (definition.RequiresApproval && (approver is null))
            {
                return null;
            }

            return new CartDiscount
            {
                Id = Guid.NewGuid(),
                DiscountId = definition.Id,
                Name = definition.Name,
                Type = definition.Type,
                Value = definition.Value,
                ApprovedByStaffId = approver?.Id
            };
        }

        var isPercent = items[index] == CustomPercent;
        var input = await dialog.InputAsync(isPercent ? "値引率 (%)" : "値引額", parameter: new PromptParameter { PromptType = PromptType.Number, MaxLength = isPercent ? 3 : 8 });
        if (!input.Accepted || !Decimal.TryParse(input.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || (value <= 0))
        {
            return null;
        }

        if (isPercent ? value > 100 : value > parameter.BaseAmount)
        {
            await dialog.InformationAsync(isPercent ? "値引率は 100% 以下にしてください。" : "値引額が金額を超えています。");
            return null;
        }

        var reason = await dialog.InputAsync("理由");
        if (!reason.Accepted || String.IsNullOrWhiteSpace(reason.Text))
        {
            await dialog.InformationAsync("任意の値引には理由が必要です。");
            return null;
        }

        return new CartDiscount
        {
            Id = Guid.NewGuid(),
            Name = isPercent ? $"値引 {value:0.#}%" : $"値引 {DisplayText.Yen(value)}",
            Type = isPercent ? DiscountType.Percent : DiscountType.Amount,
            Value = isPercent ? value / 100m : value,
            Reason = reason.Text.Trim()
        };
    }

    public static string Describe(DiscountType type, decimal value) =>
        type == DiscountType.Percent ? DisplayText.Percent(value) : DisplayText.Yen(value);

    private static async ValueTask<StaffResponseItem?> ChooseApproverAsync(IDialog dialog, DataAccessor accessor, Session session)
    {
        var staff = session.StoreId is null
            ? []
            : (await accessor.QueryStaffListAsync(session.StoreId.Value)).Where(static x => x.Role is StaffRole.Manager or StaffRole.Admin).ToList();
        if (staff.Count == 0)
        {
            await dialog.InformationAsync("承認できるスタッフ (店長・管理者) が登録されていません。");
            return null;
        }

        return await dialog.ChooseAsync(staff, static x => $"{x.Name} ({DisplayText.Name(x.Role)})", "承認者");
    }
}
