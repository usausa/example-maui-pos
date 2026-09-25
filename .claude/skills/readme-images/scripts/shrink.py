#!/usr/bin/env python3
# 撮った画面を README の大きさと色数にそろえる (管理画面は幅 1200、端末は幅 360、帳票は 600x849、256 色)
#
#   python shrink.py server <撮った画像...> [--out-dir docs/images]
#   python shrink.py terminal <撮った画像...> [--out-dir docs/images]
#   python shrink.py report <撮った画像...> [--out-dir docs/images]
#
# 帳票は PDF 表示の画面から用紙を切り出し、A4 の枠 (600x849) の中央に置く (レシートのような細い用紙は左右を灰色で埋める)。
# 名前はそのまま (server-dashboard.png → docs/images/server-dashboard.png)。Pillow が要る
import argparse
import sys
from pathlib import Path

from PIL import Image

# 帳票の枠 (A4 の比) と、用紙の外を埋める色
REPORT_SIZE = (600, 849)
REPORT_BACKGROUND = (238, 238, 238)

WIDTHS = {'server': 1200, 'terminal': 360, 'report': REPORT_SIZE[0]}


def shrink(source, destination, width):
    image = Image.open(source).convert('RGB')
    height = round(image.height * width / image.width)
    image = image.resize((width, height), Image.Resampling.LANCZOS)
    return save(image, destination)


# PDF 表示の暗い背景を除いて用紙 (白) だけを切り出し、枠に収める
def shrink_report(source, destination):
    image = Image.open(source).convert('RGB')
    box = image.convert('L').point(lambda x: 255 if x > 200 else 0).getbbox()
    if box is not None:
        image = image.crop(box)
    scale = min(REPORT_SIZE[0] / image.width, REPORT_SIZE[1] / image.height)
    page = image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))), Image.Resampling.LANCZOS)
    canvas = Image.new('RGB', REPORT_SIZE, REPORT_BACKGROUND)
    canvas.paste(page, ((REPORT_SIZE[0] - page.width) // 2, (REPORT_SIZE[1] - page.height) // 2))
    return save(canvas, destination)


def save(image, destination):
    # 画面の平らな色が縞にならないように、ディザを掛けずに減色する
    image = image.quantize(256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    image.save(destination, optimize=True)
    return image.size


def main():
    parser = argparse.ArgumentParser(description='撮った画面を README の大きさと色数にそろえる')
    parser.add_argument('kind', choices=sorted(WIDTHS))
    parser.add_argument('sources', nargs='+')
    parser.add_argument('--out-dir', default='docs/images')
    args = parser.parse_args()

    out = Path(args.out_dir)
    out.mkdir(parents=True, exist_ok=True)
    for source in args.sources:
        destination = out / Path(source).name
        size = shrink_report(source, destination) if args.kind == 'report' else shrink(source, destination, WIDTHS[args.kind])
        print(f'{destination} {size[0]}x{size[1]}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
