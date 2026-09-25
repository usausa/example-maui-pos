namespace Pos.Terminal.Models;

// 受領待ちの伝票 (入荷予定か、自店宛に出荷済みの移動)。
// Source は仕入先か出荷店の名前、Number は納品書番号 (なければ発注番号) か移動番号、Date は入荷予定日か出荷日時
public sealed record ReceivingDocument(
    ReceivingKind Kind,
    Guid Id,
    string Source,
    string? Number,
    DateOnly? ExpectedDate,
    DateTime? ShippedAt,
    string? Note,
    IReadOnlyList<ReceivingLine> Lines);

// Quantity は予定 (入荷) か出荷 (移動) の数
public sealed record ReceivingLine(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    decimal Quantity);
