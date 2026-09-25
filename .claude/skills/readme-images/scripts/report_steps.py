#!/usr/bin/env python3
# README の帳票の画像を撮る手順 (capture_server.mjs の steps.json) を作る
#
#   python report_steps.py --out DIR [--base http://localhost:8080] [--user admin] [--password admin] [--only name,...]
#
# 帳票の PDF を DIR/<name>.pdf に保存し、Chrome の PDF 表示 (操作バーなし、1 ページを画面に合わせる) で DIR/<name>.png に撮る。
# 撮るときは capture_server.mjs の画面を A4 の比 (1240 1754) にし、shrink.py report で用紙を切り出して README の大きさにする
import argparse
import json
import sys
from pathlib import Path

API = '/api/v1'

# (画像の名前, 取り出す PDF の URL を返す式)。README の帳票の表と同じ順。対象は開発用の DB から選ぶ
REPORTS = [
    # 受注票: 前受金のある未完了の受注 (なければ最初の未完了の受注)
    ('report-order', f"""(async () => {{
        const j = await (await fetch('{API}/orders?open=true&size=100')).json();
        const o = j.items.find(x => x.depositAmount > 0) ?? j.items[0];
        return '{API}/orders/' + o.id + '/pdf';
    }})()"""),
    # 発注書: 発注済みの発注 (備考のないものを選ぶ)
    ('report-purchase-order', f"""(async () => {{
        const j = await (await fetch('{API}/inventory/purchase-orders?status=Ordered&size=20')).json();
        const p = j.items.find(x => !x.note) ?? j.items[0];
        return '{API}/inventory/purchase-orders/' + p.id + '/pdf';
    }})()"""),
    # 精算レポート: 精算済みのシフト
    ('report-shift', f"""(async () => {{
        const j = await (await fetch('{API}/shifts?status=Closed&size=1')).json();
        return '{API}/shifts/' + j.items[0].id + '/summary/pdf';
    }})()"""),
    # 売上日報: 締め済みの店舗 × 営業日
    ('report-daily-sales', f"""(async () => {{
        const j = await (await fetch('{API}/daily-closings?status=Closed&size=1')).json();
        const d = j.items[0];
        return '{API}/reports/sales/daily/pdf?storeId=' + d.storeId + '&date=' + d.businessDate;
    }})()"""),
    # レシートの控え: 直近の販売のうち明細の多いもの
    ('report-receipt', f"""(async () => {{
        const j = await (await fetch('{API}/transactions?type=Sale&status=Completed&size=20')).json();
        const t = j.items.sort((a, b) => b.lines.length - a.lines.length)[0];
        return '{API}/transactions/' + t.id + '/receipt/pdf';
    }})()"""),
]


def main():
    parser = argparse.ArgumentParser(description='README の帳票の画像を撮る手順を作る')
    parser.add_argument('--out', required=True, help='PDF・画像と steps.json の置き場所')
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
    for name, url in REPORTS:
        if only is None or name in only:
            pdf = out / f'{name}.pdf'
            steps.append({'go': f'{base}/', 'waitMs': 1500})
            steps.append({'save': url, 'file': str(pdf)})
            steps.append({'go': pdf.as_uri() + '#toolbar=0&navpanes=0&view=Fit', 'waitMs': 3000})
            steps.append({'shot': str(out / f'{name}.png')})

    steps_file = out / 'steps.json'
    steps_file.write_text(json.dumps(steps, ensure_ascii=False, indent=1), encoding='utf-8')
    print(steps_file)
    return 0


if __name__ == '__main__':
    sys.exit(main())
