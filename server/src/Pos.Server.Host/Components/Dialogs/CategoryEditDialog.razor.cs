namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

public sealed partial class CategoryEditDialog
{
    private static readonly CategoryFormValidator Validator = new();

    // 親に選べるのは大分類 (2 階層まで)
    [Parameter]
    public IReadOnlyList<CategoryEntity> Parents { get; set; } = [];
}
