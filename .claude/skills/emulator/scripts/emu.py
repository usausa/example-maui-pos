#!/usr/bin/env python3
# 端末アプリをエミュレータで動かして確かめるための adb の操作 (実機は使わない)
#
#   python emu.py devices                    機器の一覧と、使うエミュレータ
#   python emu.py install [--release]        ビルドしてエミュレータに入れて起動する
#   python emu.py launch | stop              アプリを起動する / 止める
#   python emu.py shot <file.png>            画面を撮る (1080x2400)
#   python emu.py tap <x> <y>                撮った画像の座標をタップする
#   python emu.py fkey <1-4>                 画面下端の F キーを押す
#   python emu.py keypad <digits> [--ok]     電卓のシートに数字を打つ (A = AC、C = 1 字消す)
#   python emu.py text <ascii>               文字を入力する (英数字と記号だけ)
#   python emu.py key <BACK|ENTER|DEL|...>   キーを送る
#   python emu.py swipe <x1> <y1> <x2> <y2> [ms]
#   python emu.py airplane <on|off>          機内モード (オフラインの確認)
#   python emu.py screen                     今の画面の遷移 (logcat の Navigated) を出す
#   python emu.py logcat [--lines N] [--grep 正規表現]
#   python emu.py pref get <key>             アプリの設定 (shared_prefs) の値を出す
#   python emu.py pref set <key> <value>     アプリの設定の文字列を書き換える (アプリを止めてから)
#
# 使う機器は ANDROID_SERIAL (emulator- で始まるものだけ) か、接続中の最初のエミュレータ。adb の場所は ADB で変えられる
import argparse
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
PACKAGE = 'pos.terminal'
PROJECT = 'terminal/Pos.Terminal/Pos.Terminal.csproj'
FRAMEWORK = 'net10.0-android'

# 1080x2400 (Pixel 6a 相当) の配置。変わったら撮った画像で確かめる
FUNCTION_KEYS = {1: (135, 2264), 2: (405, 2264), 3: (675, 2264), 4: (945, 2264)}
KEYPAD = {
    '1': (186, 1754), '2': (540, 1754), '3': (894, 1754),
    '4': (186, 1878), '5': (540, 1878), '6': (894, 1878),
    '7': (186, 2000), '8': (540, 2000), '9': (894, 2000),
    'A': (186, 2120), '0': (540, 2120), 'C': (894, 2120),
}
KEYPAD_OK = (810, 2264)


def find_adb():
    candidates = [os.environ.get('ADB'), shutil.which('adb')]
    for home in (os.environ.get('ANDROID_HOME'), os.environ.get('ANDROID_SDK_ROOT')):
        if home:
            candidates.append(str(Path(home) / 'platform-tools' / 'adb'))
    candidates += [
        'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe',
        str(Path(os.environ.get('LOCALAPPDATA', '')) / 'Android/Sdk/platform-tools/adb.exe'),
    ]
    for candidate in candidates:
        if candidate and (shutil.which(candidate) or Path(candidate).exists()):
            return candidate
    sys.exit('adb が見つかりません (環境変数 ADB で場所を指定する)')


ADB = find_adb()


def devices():
    output = subprocess.run([ADB, 'devices'], capture_output=True, text=True, check=True).stdout
    return [line.split('\t') for line in output.splitlines()[1:] if '\t' in line]


def emulator_serial():
    # 実機に入れたり操作したりしないように、エミュレータ (emulator-) だけを選ぶ
    serial = os.environ.get('ANDROID_SERIAL')
    if serial:
        if not serial.startswith('emulator-'):
            sys.exit(f'ANDROID_SERIAL がエミュレータではありません: {serial}')
        return serial
    for serial, state in devices():
        if serial.startswith('emulator-') and state == 'device':
            return serial
    sys.exit('起動しているエミュレータがありません')


def adb(*args, capture=False):
    command = [ADB, '-s', emulator_serial(), *args]
    if capture:
        return subprocess.run(command, capture_output=True, check=True).stdout
    return subprocess.run(command, check=True)


def shell(command, capture=True):
    output = adb('shell', command, capture=capture)
    return output.decode('utf-8', errors='replace') if capture else None


def pause(seconds=1.2):
    # 操作の結果が画面に出るまで待つ
    time.sleep(seconds)


#--------------------------------------------------------------------------------
# App
#--------------------------------------------------------------------------------

def install(release):
    serial = emulator_serial()
    configuration = 'Release' if release else 'Debug'
    print(f'{serial} に {configuration} を入れて起動します', flush=True)
    return subprocess.run(['dotnet', 'build', PROJECT, '-f', FRAMEWORK, '-c', configuration, '-t:Run', f'-p:AdbTarget=-s {serial}'], cwd=ROOT, check=False).returncode


def launch():
    shell(f'monkey -p {PACKAGE} -c android.intent.category.LAUNCHER 1')


def stop():
    shell(f'am force-stop {PACKAGE}')


#--------------------------------------------------------------------------------
# Screen
#--------------------------------------------------------------------------------

def shot(file):
    data = adb('exec-out', 'screencap', '-p', capture=True)
    Path(file).write_bytes(data)
    print(file)


def tap(x, y):
    shell(f'input tap {x} {y}', capture=False)
    pause()


def keypad(digits, ok):
    for digit in digits.upper():
        if digit not in KEYPAD:
            sys.exit(f'電卓にないキーです: {digit}')
        x, y = KEYPAD[digit]
        shell(f'input tap {x} {y}', capture=False)
    if ok:
        shell(f'input tap {KEYPAD_OK[0]} {KEYPAD_OK[1]}', capture=False)
    pause()


def text(value):
    if not re.fullmatch(r'[\x21-\x7e]+', value):
        sys.exit('text は空白のない ASCII だけを送れる (日本語は送れない)')
    # 端末のシェルで解釈される記号を逃がす
    escaped = re.sub(r'([\\\'"`$&|;<>()*?~#%])', r'\\\1', value)
    shell(f'input text {escaped}', capture=False)
    pause(0.6)


def logcat(lines, pattern):
    output = adb('logcat', '-d', '-t', str(lines), capture=True).decode('utf-8', errors='replace')
    for line in output.splitlines():
        if pattern is None or re.search(pattern, line):
            print(line)


#--------------------------------------------------------------------------------
# Preferences
#--------------------------------------------------------------------------------

def preference_files():
    output = shell(f'run-as {PACKAGE} ls shared_prefs')
    return [name.strip() for name in output.split() if name.strip().endswith('.xml')]


def pref_get(key):
    for name in preference_files():
        content = shell(f'run-as {PACKAGE} cat shared_prefs/{name}')
        for match in re.finditer(r'<(\w+) name="' + re.escape(key) + r'"(?: value="([^"]*)")?\s*/?>(?:([^<]*)</\1>)?', content):
            print(f'{name}: {match.group(2) if match.group(2) is not None else match.group(3)}')


def pref_set(key, value):
    if re.search(r"['#<>&\\]", value) or re.search(r"['#<>&\\\"]", key):
        sys.exit('キーと値に使えない文字が入っています')
    # アプリが動いていると終了時に書き戻されるので、止めてから書き換える
    stop()
    changed = 0
    for name in preference_files():
        content = shell(f'run-as {PACKAGE} cat shared_prefs/{name}')
        if f'<string name="{key}">' in content:
            shell(f"run-as {PACKAGE} sed -i 's#<string name=\"{key}\">[^<]*</string>#<string name=\"{key}\">{value}</string>#' shared_prefs/{name}")
            changed += 1
    print(f'{key} を {changed} ファイルで書き換えました')
    pref_get(key)


#--------------------------------------------------------------------------------
# Main
#--------------------------------------------------------------------------------

def main():
    if not sys.stdout.isatty():
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    parser = argparse.ArgumentParser(description='端末アプリをエミュレータで確かめるための adb の操作')
    sub = parser.add_subparsers(dest='command', required=True)
    sub.add_parser('devices')
    p = sub.add_parser('install')
    p.add_argument('--release', action='store_true')
    sub.add_parser('launch')
    sub.add_parser('stop')
    p = sub.add_parser('shot')
    p.add_argument('file')
    p = sub.add_parser('tap')
    p.add_argument('x', type=int)
    p.add_argument('y', type=int)
    p = sub.add_parser('fkey')
    p.add_argument('number', type=int, choices=[1, 2, 3, 4])
    p = sub.add_parser('keypad')
    p.add_argument('digits')
    p.add_argument('--ok', action='store_true')
    p = sub.add_parser('text')
    p.add_argument('value')
    p = sub.add_parser('key')
    p.add_argument('name')
    p = sub.add_parser('swipe')
    p.add_argument('coords', type=int, nargs=4)
    p.add_argument('ms', type=int, nargs='?', default=300)
    p = sub.add_parser('airplane')
    p.add_argument('state', choices=['on', 'off'])
    sub.add_parser('screen')
    p = sub.add_parser('logcat')
    p.add_argument('--lines', type=int, default=200)
    p.add_argument('--grep')
    p = sub.add_parser('pref')
    p.add_argument('action', choices=['get', 'set'])
    p.add_argument('key')
    p.add_argument('value', nargs='?')
    args = parser.parse_args()

    if args.command == 'devices':
        for serial, state in devices():
            print(f'{serial}\t{state}\t{"(使わない)" if not serial.startswith("emulator-") else ""}')
        print(f'使うエミュレータ: {emulator_serial()}')
    elif args.command == 'install':
        return install(args.release)
    elif args.command == 'launch':
        launch()
    elif args.command == 'stop':
        stop()
    elif args.command == 'shot':
        shot(args.file)
    elif args.command == 'tap':
        tap(args.x, args.y)
    elif args.command == 'fkey':
        tap(*FUNCTION_KEYS[args.number])
    elif args.command == 'keypad':
        keypad(args.digits, args.ok)
    elif args.command == 'text':
        text(args.value)
    elif args.command == 'key':
        shell(f'input keyevent {args.name if args.name.isdigit() else "KEYCODE_" + args.name.upper()}', capture=False)
        pause(0.6)
    elif args.command == 'swipe':
        x1, y1, x2, y2 = args.coords
        shell(f'input swipe {x1} {y1} {x2} {y2} {args.ms}', capture=False)
        pause()
    elif args.command == 'airplane':
        shell(f'cmd connectivity airplane-mode {"enable" if args.state == "on" else "disable"}', capture=False)
        pause(2)
    elif args.command == 'screen':
        logcat(500, r'Navigated:')
    elif args.command == 'logcat':
        logcat(args.lines, args.grep)
    elif args.command == 'pref':
        if args.action == 'get':
            pref_get(args.key)
        else:
            if args.value is None:
                sys.exit('pref set には値が要る')
            pref_set(args.key, args.value)
    return 0


if __name__ == '__main__':
    sys.exit(main())
