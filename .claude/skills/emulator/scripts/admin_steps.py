#!/usr/bin/env python3
# エミュレータで確かめるときの管理画面の操作 (ペアリングコードの発行、登録の解除) の手順を作る。
# 管理画面はフォームでログインするので API では操作できない。手順は readme-images の capture_server.mjs で実行する
#
#   python admin_steps.py pair --out DIR [--base URL] [--terminal 名称]     コードを発行し、eval の出力に 6 桁を出す
#   python admin_steps.py revoke --out DIR [--base URL] [--terminal 名称]   サーバ側の登録を解除する (登録がなければ何もしない)
#   node .claude/skills/readme-images/scripts/capture_server.mjs DIR/steps.json
import argparse
import json
import sys
from pathlib import Path


def login_steps(base, user, password):
    return [
        {'go': f'{base}/login', 'waitMs': 2000},
        {'click': '', 'selector': 'input[name=name]'}, {'type': user},
        {'click': '', 'selector': 'input[name=password]'}, {'type': password},
        {'click': 'ログイン', 'selector': 'button', 'waitMs': 3000},
        {'go': f'{base}/terminals', 'waitMs': 3000},
    ]


# 端末の行を名称で探す (名称は json.dumps で JavaScript の文字列にする)
def find_row(name):
    return (f"const name = {json.dumps(name)}; "
            "const row = [...document.querySelectorAll('tbody tr')].find(r => r.innerText.includes(name)); "
            "if (!row) return 'not found: ' + name;")


# 操作の列の最初のボタンがペアリングコードの発行。発行するとダイアログに 6 桁が出る
def pair_steps(name):
    script = ("(async () => { " + find_row(name) + " "
              "row.querySelector('td:last-child button').click(); "
              "for (let i = 0; i < 40; i++) { "
              "await new Promise(r => setTimeout(r, 250)); "
              "const code = document.querySelector('.pairing-code'); "
              "if (code && code.innerText.trim()) return 'code: ' + code.innerText.trim(); } "
              "return 'code not shown'; })()")
    return [{'eval': script}]


# 登録中の行だけに登録の解除 (警告色のボタン) が出る。確認のダイアログは OK で閉じる
def revoke_steps(name):
    script = ("(async () => { " + find_row(name) + " "
              "const revoke = row.querySelector('button.mud-warning-text'); "
              "if (!revoke) return 'not registered: ' + name; "
              "revoke.click(); "
              "for (let i = 0; i < 40; i++) { "
              "await new Promise(r => setTimeout(r, 250)); "
              "const ok = [...document.querySelectorAll('.mud-dialog button')].find(b => b.innerText.trim() === 'OK'); "
              "if (ok) { ok.click(); await new Promise(r => setTimeout(r, 2000)); return 'revoked: ' + name; } } "
              "return 'confirm not shown'; })()")
    return [{'eval': script}]


def main():
    parser = argparse.ArgumentParser(description='管理画面でペアリングコードを発行する / 登録を解除する手順を作る')
    parser.add_argument('action', choices=['pair', 'revoke'])
    parser.add_argument('--out', required=True, help='steps.json と撮った画像の置き場所')
    parser.add_argument('--base', default='http://localhost:8080', help='サーバの URL')
    parser.add_argument('--terminal', default='本店 レジ 2', help='レジ端末の一覧の名称')
    parser.add_argument('--user', default='admin')
    parser.add_argument('--password', default='admin')
    args = parser.parse_args()

    out = Path(args.out).resolve()
    out.mkdir(parents=True, exist_ok=True)
    steps = login_steps(args.base.rstrip('/'), args.user, args.password)
    steps += pair_steps(args.terminal) if args.action == 'pair' else revoke_steps(args.terminal)
    steps.append({'shot': str(out / f'admin-{args.action}.png')})

    steps_file = out / 'steps.json'
    steps_file.write_text(json.dumps(steps, ensure_ascii=False, indent=1), encoding='utf-8')
    print(steps_file)
    return 0


if __name__ == '__main__':
    sys.exit(main())
