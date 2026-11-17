---
paths:
  - "shared/Pos.Domain/**"
---
# Pos.Domain

- 業務ルールは `Pos.Domain.Logic` に `SalesLogic`、`ReturnLogic` のような業務ごとの静的クラスで置く (フォルダには分けない)
- 列挙型は `Pos.Domain.Enums` に 1 型 1 ファイルで置く (通信データで使う列挙型も含む)
- Domain は I/O やフレームワークに依存しない
- Domain のパッケージは `Usa.Smart.Core` だけを参照する (分析器を除く)
- Logic は時刻を引数で受ける (`DateTime.Now` を使わない)
- 検証の `ValidateXxx` は `RuleError?` を返す (null なら正常)
- 取引の検証は `TransactionValidation` を返す
- 入力の検証 (`TransactionLogic.ValidateInput`) だけは `RuleError` の一覧を返す
- DB の事実は、呼び出し側が `XxxFact` / `XxxContext` に詰めて渡す
- 明細を持つ計算 (販売、返品) は `XxxInput` を受けて `XxxResult` を返す (どちらも record)
- 業務ルールの違反は `RuleError` (`ErrorCode` + `RuleReason`) で返す
- Domain に文言を置かず、使う側が `RuleReason` から文言を引く
