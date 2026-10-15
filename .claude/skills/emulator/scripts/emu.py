#!/usr/bin/env python3
# 端末アプリをエミュレータで動かして確かめるための adb の操作 (実機は使わない)
#
#   python emu.py devices                    機器の一覧と、使うエミュレータ (画面の大きさも確かめる)
#   python emu.py boot <avd>                 エミュレータを起動して、起動が終わるまで待つ (起動済みなら待つだけ)
#   python emu.py shutdown                   エミュレータを終了する
#   python emu.py install [--release]        ビルドしてエミュレータに入れて起動する
#   python emu.py launch | stop              アプリを起動する / 止める
#   python emu.py shot <file.png>            画面を撮る (1080x2400)
#   python emu.py tap <x> <y>                撮った画像の座標をタップする
#   python emu.py fkey <1-4>                 画面下端の F キーを押す
#   python emu.py keypad [digits] [--ok]     電卓のシートに数字を打つ (A = AC、C = 1 字消す。--ok だけで確定)
#   python emu.py text <ascii>               文字を入力する (英数字と記号だけ)
#   python emu.py key <BACK|ENTER|DEL|...> [--repeat N]   キーを送る (入力欄を消すときは DEL を繰り返す)
#   python emu.py swipe <x1> <y1> <x2> <y2> [ms]
#   python emu.py airplane <on|off>          機内モード (オフラインの確認)
#   python emu.py camera <status|grant|revoke|deny>   カメラの許可を見る / 付ける / 取り消す (取り消すと次の起動で聞き直す) / ダイアログを断る
#   python emu.py screen                     今の画面の遷移 (logcat の Navigated) を出す
#   python emu.py logcat [--lines N] [--grep 正規表現]
#   python emu.py pref get <key>             アプリの設定 (shared_prefs) の値を出す
#   python emu.py pref set <key> <value>     アプリの設定の文字列を書き換える (動いているアプリは止めてから書き換える。キーがなければ足す)
#
# 使う機器は ANDROID_SERIAL (emulator- で始まるものだけ) か、接続中の最初のエミュレータ。
# adb の場所は ADB、エミュレータの場所は EMULATOR で変えられる
import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
PACKAGE = 'pos.terminal'
# Settings (IPreferences) が使う既定の設定ファイル
DEFAULT_PREFERENCES = f'{PACKAGE}_preferences.xml'
PROJECT = 'terminal/Pos.Terminal/Pos.Terminal.csproj'
FRAMEWORK = 'net10.0-android'

# 1080x2400 (Pixel 6a 相当) の配置。変わったら撮った画像で確かめる
SCREEN_SIZE = '1080x2400'
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


def find_emulator():
    candidates = [os.environ.get('EMULATOR'), shutil.which('emulator')]
    for home in (os.environ.get('ANDROID_HOME'), os.environ.get('ANDROID_SDK_ROOT')):
        if home:
            candidates.append(str(Path(home) / 'emulator' / 'emulator'))
    # adb と同じ SDK の emulator
    candidates.append(str(Path(ADB).resolve().parents[1] / 'emulator' / 'emulator'))
    for candidate in candidates:
        if not candidate:
            continue
        for path in (candidate, candidate + '.exe'):
            if shutil.which(path) or Path(path).is_file():
                return path
    sys.exit('emulator が見つかりません (環境変数 EMULATOR で場所を指定する)')


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


def check_screen_size():
    # 座標 (F キー、電卓) は 1080x2400 の配置なので、違う機器では撮った画像で座標を確かめる
    output = shell('wm size')
    sizes = re.findall(r'(\d+x\d+)', output)
    size = sizes[-1] if sizes else '?'
    if size == SCREEN_SIZE:
        print(f'画面: {size}')
    else:
        print(f'画面: {size} (座標は {SCREEN_SIZE} の配置。撮った画像で座標を確かめる)')


def pause(seconds=1.2):
    # 操作の結果が画面に出るまで待つ
    time.sleep(seconds)


#--------------------------------------------------------------------------------
# Emulator
#--------------------------------------------------------------------------------

def boot(avd, timeout):
    running = [serial for serial, _ in devices() if serial.startswith('emulator-')]
    if not running:
        emulator = find_emulator()
        avds = subprocess.run([emulator, '-list-avds'], capture_output=True, text=True, check=False).stdout.split()
        if avd is None or avd not in avds:
            sys.exit(f'AVD の名前を渡す: {" / ".join(avds) if avds else "(AVD がない)"}')
        # このスクリプトが終わっても動き続けるように、切り離して起動する
        options = {'creationflags': subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == 'nt' else {'start_new_session': True}
        subprocess.Popen([emulator, '-avd', avd], stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, **options)
        print(f'{avd} を起動しています', flush=True)
    deadline = time.time() + timeout
    while time.time() < deadline:
        ready = [serial for serial, state in devices() if serial.startswith('emulator-') and state == 'device']
        if ready:
            completed = subprocess.run([ADB, '-s', ready[0], 'shell', 'getprop', 'sys.boot_completed'], capture_output=True, text=True, check=False).stdout.strip()
            if completed == '1':
                print(f'起動しました: {ready[0]}')
                check_screen_size()
                return 0
        time.sleep(3)
    sys.exit(f'{timeout} 秒で起動が終わりませんでした')


def shutdown():
    serial = emulator_serial()
    subprocess.run([ADB, '-s', serial, 'emu', 'kill'], check=False)
    print(f'{serial} を終了しました')


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


def permission_dialog_shown():
    # 前面のウインドウが許可のダイアログ (GrantPermissionsActivity) か
    focus = re.search(r'mCurrentFocus=(.*)', shell('dumpsys window'))
    return focus is not None and 'GrantPermissionsActivity' in focus.group(1)


def camera(action, timeout):
    permission = 'android.permission.CAMERA'
    if action == 'grant':
        shell(f'pm grant {PACKAGE} {permission}', capture=False)
    elif action == 'revoke':
        # 「今後表示しない」の印も消して、次の起動で許可を聞き直す元の状態に戻す
        shell(f'pm revoke {PACKAGE} {permission}', capture=False)
        shell(f'pm clear-permission-flags {PACKAGE} {permission} user-set user-fixed', capture=False)
    elif action == 'deny':
        # 戻るで断る (座標で押すと許可に当たることがある)。ダイアログがないときに送るとアプリが背面へ回るので、出るまで待つ
        deadline = time.time() + timeout
        while not permission_dialog_shown() and time.time() < deadline:
            time.sleep(1)
        if permission_dialog_shown():
            shell('input keyevent KEYCODE_BACK', capture=False)
            pause(0.6)
        else:
            print('許可のダイアログは出ていません')
    granted = re.search(re.escape(permission) + r': granted=(\w+)', shell(f'dumpsys package {PACKAGE}'))
    print(f'カメラの許可: {granted.group(1) if granted else "未設定"}')


#--------------------------------------------------------------------------------
# Preferences
#--------------------------------------------------------------------------------

def preference_files():
    # 入れ直した直後は shared_prefs がない
    output = shell(f'run-as {PACKAGE} ls shared_prefs 2>/dev/null || true')
    return [name.strip() for name in output.split() if name.strip().endswith('.xml')]


def add_preference(key, value):
    # キーがない (入れ直した直後) ときは、Settings が使う既定の設定ファイルに足す (ファイルがなければ作る)
    entry = f'<string name="{key}">{value}</string>'
    if DEFAULT_PREFERENCES in preference_files():
        shell(f"run-as {PACKAGE} sed -i 's#</map>#    {entry}\\n</map>#' shared_prefs/{DEFAULT_PREFERENCES}")
        return
    temp = Path(tempfile.gettempdir()) / DEFAULT_PREFERENCES
    temp.write_text(f"<?xml version='1.0' encoding='utf-8' standalone='yes' ?>\n<map>\n    {entry}\n</map>\n", encoding='utf-8', newline='\n')
    adb('push', str(temp), f'/data/local/tmp/{DEFAULT_PREFERENCES}', capture=True)
    temp.unlink()
    shell(f'run-as {PACKAGE} mkdir -p shared_prefs')
    shell(f'run-as {PACKAGE} cp /data/local/tmp/{DEFAULT_PREFERENCES} shared_prefs/{DEFAULT_PREFERENCES}')
    shell(f'run-as {PACKAGE} chmod 660 shared_prefs/{DEFAULT_PREFERENCES}')
    shell(f'rm /data/local/tmp/{DEFAULT_PREFERENCES}')


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
    if changed:
        print(f'{key} を {changed} ファイルで書き換えました')
    else:
        add_preference(key, value)
        print(f'{key} を {DEFAULT_PREFERENCES} に足しました')
    pref_get(key)


#--------------------------------------------------------------------------------
# Main
#--------------------------------------------------------------------------------

def main():
    for stream in (sys.stdout, sys.stderr):
        if not stream.isatty():
            stream.reconfigure(encoding='utf-8', errors='replace')
    parser = argparse.ArgumentParser(description='端末アプリをエミュレータで確かめるための adb の操作')
    sub = parser.add_subparsers(dest='command', required=True)
    sub.add_parser('devices')
    p = sub.add_parser('boot')
    p.add_argument('avd', nargs='?')
    p.add_argument('--timeout', type=int, default=300)
    sub.add_parser('shutdown')
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
    p.add_argument('digits', nargs='?', default='')
    p.add_argument('--ok', action='store_true')
    p = sub.add_parser('text')
    p.add_argument('value')
    p = sub.add_parser('key')
    p.add_argument('name')
    p.add_argument('--repeat', type=int, default=1)
    p = sub.add_parser('swipe')
    p.add_argument('coords', type=int, nargs=4)
    p.add_argument('ms', type=int, nargs='?', default=300)
    p = sub.add_parser('airplane')
    p.add_argument('state', choices=['on', 'off'])
    p = sub.add_parser('camera')
    p.add_argument('action', choices=['status', 'grant', 'revoke', 'deny'])
    p.add_argument('--timeout', type=int, default=10)
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
        check_screen_size()
    elif args.command == 'boot':
        return boot(args.avd, args.timeout)
    elif args.command == 'shutdown':
        shutdown()
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
        code = args.name if args.name.isdigit() else 'KEYCODE_' + args.name.upper()
        shell('input keyevent ' + ' '.join([code] * max(1, args.repeat)), capture=False)
        pause(0.6)
    elif args.command == 'swipe':
        x1, y1, x2, y2 = args.coords
        shell(f'input swipe {x1} {y1} {x2} {y2} {args.ms}', capture=False)
        pause()
    elif args.command == 'airplane':
        shell(f'cmd connectivity airplane-mode {"enable" if args.state == "on" else "disable"}', capture=False)
        pause(2)
    elif args.command == 'camera':
        camera(args.action, args.timeout)
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
