---
paths:
  - "docs/**"
  - "README.md"
---
# 文書

- `docs/` は人間向けの設計文書。  
  現状 (何を・なぜ) を説明し、どう実装すべきかの規則は書かない (規則は `AGENTS.md` と `.claude/rules/`)
- 経緯と設計判断の履歴は `docs/decisions.md` にだけ書き、日付は書かない。  
  後回しにした項目は `docs/implementation-plan.md`
- 設計判断は `docs/decisions.md` に記録し、フェーズを閉じる前に該当する設計文書を更新する
- Markdown は「。」  
  で文を終え、2 スペースで改行する (表・見出し・コードは除く)
- README には主要な画面と文書へのリンクだけを載せる (画面 ID や手順は書かない)
- 外部の参考資料へのリンクは載せない
