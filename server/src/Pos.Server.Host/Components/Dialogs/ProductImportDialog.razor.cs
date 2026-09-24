namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

using MudBlazor;

using Pos.Server.Host.Infrastructure.Csv;
using Pos.Server.Host.Models.Import;
using Pos.Server.Models.Parameters;
using Pos.Server.Services;

// 商品 CSV の取込。ファイルを選ぶとプレビュー (dryRun) を出し、誤りがなく変更がある行があれば [取込] で反映して結果を返す
public sealed partial class ProductImportDialog
{
    private IReadOnlyList<ProductImportLine> lines = [];
    private ProductImportResult? preview;
    private string? fileName;
    private string? message;
    private bool showUnchanged;
    private bool busy;

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required ProductService ProductService { get; set; }

    // 誤りがなく、登録か更新がある
    private bool CanImport => preview is { Status: DataWriteStatus.Success } && ((preview.InsertCount + preview.UpdateCount) > 0);

    private IEnumerable<ProductImportLineResult> VisibleLines =>
        showUnchanged ? preview!.Lines : preview!.Lines.Where(static x => x.Action != ImportAction.Unchanged);

    private async Task OnFileSelectedAsync(IBrowserFile? file)
    {
        preview = null;
        message = null;
        lines = [];
        if (file is null)
        {
            return;
        }

        fileName = file.Name;
        if (file.Size > CsvImport.MaxBytes)
        {
            message = "CSV は 5 MB までにしてください。";
            return;
        }

        busy = true;
        try
        {
            using var buffer = new MemoryStream();
            await using (var stream = file.OpenReadStream(CsvImport.MaxBytes))
            {
                await stream.CopyToAsync(buffer);
            }

            var csv = CsvImport.Read<ProductImportRow>(buffer.ToArray());
            if (csv.MissingHeaders.Count > 0)
            {
                message = $"CSV の列が足りません: {String.Join(", ", csv.MissingHeaders)}";
                return;
            }

            if (csv.Rows.Count == 0)
            {
                message = "取り込む行がありません。";
                return;
            }

            lines = ProductImportRow.ToLines(csv.Rows);
            preview = await ProductService.ImportAsync(lines, true, CancellationToken.None);
        }
        finally
        {
            busy = false;
        }
    }

    // 反映のときも同じ検証をやり直す。プレビューの後に他で変わっていれば結果を出し直す
    private async Task ImportAsync()
    {
        busy = true;
        try
        {
            var result = await ProductService.ImportAsync(lines, false, CancellationToken.None);
            if (result.Status == DataWriteStatus.Success)
            {
                MudDialog.Close(DialogResult.Ok(result));
                return;
            }

            message = result.Status == DataWriteStatus.VersionMismatch
                ? "取り込んでいる間に他で商品が更新されました。結果を確かめてから、もう一度取り込んでください。"
                : "取り込めない行があります。";
            preview = await ProductService.ImportAsync(lines, true, CancellationToken.None);
        }
        finally
        {
            busy = false;
        }
    }
}
