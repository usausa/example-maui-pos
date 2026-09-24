namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Infrastructure.Imaging;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

// 商品の追加・編集。画像は選んだものをプレビューに出し、[保存] のあとにページが反映する (キャンセルで破棄)
public sealed partial class ProductEditDialog
{
    private static readonly ProductFormValidator Validator = new();

    private List<CategoryEntity> categories = [];
    private List<TaxRateEntity> taxRates = [];

    // 登録済みの画像の URL か、選んだ画像の data URL
    private string? imagePreview;
    private string? imageError;

    // 登録済みの画像 (ImageUrl。追加のときは null)
    [Parameter]
    public string? CurrentImage { get; set; }

    [Inject]
    public required CategoryService CategoryService { get; set; }

    [Inject]
    public required TaxRateService TaxRateService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        imagePreview = CurrentImage;
        categories = await CategoryService.QueryAllAsync(false, CancellationToken.None);
        taxRates = await TaxRateService.QueryListAsync(null, false, CancellationToken.None);
    }

    private async Task OnImageSelectedAsync(IBrowserFile? file)
    {
        imageError = null;
        if (file is null)
        {
            return;
        }

        if (file.Size > ProductService.ImageMaxBytes)
        {
            imageError = "画像は 2 MB までにしてください";
            return;
        }

        using var buffer = new MemoryStream();
        await using (var stream = file.OpenReadStream(ProductService.ImageMaxBytes))
        {
            await stream.CopyToAsync(buffer);
        }

        var data = buffer.ToArray();
        var contentType = ImageContentType.Detect(data);
        if (contentType is null)
        {
            imageError = "JPEG か PNG の画像を選んでください";
            return;
        }

        Form.NewImage = data;
        Form.RemoveImage = false;
        imagePreview = $"data:{contentType};base64,{Convert.ToBase64String(data)}";
    }

    private void OnImageRemoveClick()
    {
        Form.NewImage = null;
        Form.RemoveImage = CurrentImage is not null;
        imagePreview = null;
        imageError = null;
    }
}
