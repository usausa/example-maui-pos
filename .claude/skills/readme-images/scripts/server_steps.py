#!/usr/bin/env python3
# README の管理画面の画像を撮る手順 (capture_server.mjs の steps.json) を作る
#
#   python server_steps.py --out DIR [--base http://localhost:8080] [--user admin] [--password admin] [--only name,...]
#
# 撮った画像は DIR/<name>.png (1400x900)。shrink.py で README の大きさにして docs/images/ に置く
import argparse
import json
import sys
from pathlib import Path

# (画像の名前, 経路)。README の管理画面の表と同じ順
PAGES = [
    ('server-dashboard', ''),
    ('server-sales-report', 'reports/sales'),
    ('server-transactions', 'transactions'),
    ('server-orders', 'orders'),
    ('server-daily-closings', 'daily-closings'),
    ('server-products', 'products'),
    ('server-inventory-receipts', 'inventory/receipts'),
    ('server-inventory-transfers', 'inventory/transfers'),
    ('server-accounts', 'accounts'),
]

# レジ端末はペアリングコードのダイアログを開いた状態で撮る (未登録の端末にコードを発行する)
TERMINAL_ROW = '本店 レジ 2'


def main():
    parser = argparse.ArgumentParser(description='README の管理画面の画像を撮る手順を作る')
    parser.add_argument('--out', required=True, help='画像と steps.json の置き場所')
    parser.add_argument('--base', default='http://localhost:8080', help='サーバの URL')
    parser.add_argument('--user', default='admin')
    parser.add_argument('--password', default='admin')
    parser.add_argument('--only', help='撮る画像の名前 (カンマ区切り)。省くとすべて')
    args = parser.parse_args()

    base = args.base.rstrip('/')
    out = Path(args.out).resolve()
    out.mkdir(parents=True, exist_ok=True)
    only = set(args.only.split(',')) if args.only else None

    steps = [
        {'go': f'{base}/login', 'waitMs': 2000},
        {'click': '', 'selector': 'input[name=name]'}, {'type': args.user},
        {'click': '', 'selector': 'input[name=password]'}, {'type': args.password},
        {'click': 'ログイン', 'selector': 'button', 'waitMs': 3000},
    ]
    for name, path in PAGES:
        if only is None or name in only:
            steps.append({'go': f'{base}/{path}', 'waitMs': 4000})
            steps.append({'shot': str(out / f'{name}.png')})
    if only is None or 'server-terminals' in only:
        steps.append({'go': f'{base}/terminals', 'waitMs': 3000})
        steps.append({'eval': f"[...document.querySelectorAll('tbody tr')].find(r => r.innerText.includes('{TERMINAL_ROW}')).querySelector('button').click(); 'issued'"})
        steps.append({'wait': 2500})
        steps.append({'shot': str(out / 'server-terminals.png')})

    steps_file = out / 'steps.json'
    steps_file.write_text(json.dumps(steps, ensure_ascii=False, indent=1), encoding='utf-8')
    print(steps_file)
    return 0


if __name__ == '__main__':
    sys.exit(main())
