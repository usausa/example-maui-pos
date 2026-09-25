namespace Pos.Contract.Terminals;

using Pos.Contract.Stores;

public sealed class TerminalPairResponse
{
    // 以後の要求に Authorization: Bearer で付ける。サーバはハッシュだけを持ち、再発行しない
    public string Token { get; set; } = default!;

    public TerminalResponseItem Terminal { get; set; } = default!;

    public StoreResponseItem Store { get; set; } = default!;
}
