namespace Pos.Server.Services;

using Pos.Server.Models.Entity;

// 棚卸・調整の結果。同じ id の再送は Duplicate (登録済みの内容を返す)
public sealed record InventoryChangeResult(InventoryChangeEntity Change, bool Duplicate);
