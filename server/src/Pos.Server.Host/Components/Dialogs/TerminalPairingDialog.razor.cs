namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Models.Entity;
using Pos.Server.Services;

// 端末の登録: 発行したペアリングコード (端末は印刷した設定 QR を読んでから、このコードを入れる)
public sealed partial class TerminalPairingDialog
{
    [Parameter]
    public required TerminalEntity Terminal { get; set; }

    [Parameter]
    public required string StoreName { get; set; }

    [Parameter]
    public required TerminalPairingCode Code { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }
}
