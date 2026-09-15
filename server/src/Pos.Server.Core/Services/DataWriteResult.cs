namespace Pos.Server.Services;

// 更新の結果 (成功なら更新後の行)
public sealed record DataWriteResult<T>(DataWriteStatus Status, T? Entity)
    where T : class;
