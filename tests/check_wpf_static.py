#!/usr/bin/env python3
"""Kiểm tra TĨNH giao diện WPF (không cần Windows/WPF để chạy):
  1. Mọi file .xaml là XML hợp lệ.
  2. Mọi x:Name trong XAML được dùng/khớp với code-behind, mọi tên điều khiển trong code-behind đều có trong XAML.
  3. Mọi trình xử lý sự kiện (Click=, SelectionChanged=, ...) trong XAML có phương thức tương ứng trong code-behind.
  4. Mọi StaticResource/DynamicResource tham chiếu tới khóa có thật (App.xaml, Themes/*.xaml hoặc khóa cục bộ).
  5. Hai theme Sáng/Tối có cùng tập khóa.
  6. x:Class khớp với lớp partial trong code-behind.
Chạy:  python3 tests/check_wpf_static.py
"""
import re, sys, glob, os
import xml.etree.ElementTree as ET

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'src', 'OjGen.App')
X = '{http://schemas.microsoft.com/winfx/2006/xaml}'
errors = []

def err(msg): errors.append(msg)

xamls = sorted(glob.glob(os.path.join(ROOT, '**', '*.xaml'), recursive=True))
trees = {}
for f in xamls:
    try:
        trees[f] = ET.parse(f)
    except ET.ParseError as e:
        err(f'{os.path.relpath(f, ROOT)}: XML không hợp lệ: {e}')

# --- khóa tài nguyên
def keys_of(tree):
    ks = set()
    for el in tree.getroot().iter():
        k = el.get(X + 'Key')
        if k: ks.add(k)
    return ks

res_keys = set()
theme_keys = {}
for f, t in trees.items():
    ks = keys_of(t)
    base = os.path.basename(f)
    if base in ('LightTheme.xaml', 'DarkTheme.xaml'):
        theme_keys[base] = ks
    res_keys |= ks
if 'LightTheme.xaml' in theme_keys and 'DarkTheme.xaml' in theme_keys:
    a, b = theme_keys['LightTheme.xaml'], theme_keys['DarkTheme.xaml']
    if a != b: err(f'Theme Sáng/Tối lệch khóa: chỉ ở Sáng={sorted(a-b)}, chỉ ở Tối={sorted(b-a)}')

ref_re = re.compile(r'\{(StaticResource|DynamicResource)\s+([A-Za-z0-9_]+)\}')
for f in xamls:
    text = open(f, encoding='utf-8-sig').read()
    local = keys_of(trees[f]) if f in trees else set()
    for m in ref_re.finditer(text):
        k = m.group(2)
        if k not in res_keys and k not in local:
            err(f'{os.path.relpath(f, ROOT)}: tham chiếu tài nguyên không tồn tại: {k}')

# --- code-behind
event_attrs = {'Click','SelectionChanged','TextChanged','Checked','Unchecked','Drop','PreviewDragOver','DragOver',
               'Loaded','Closing','Closed','KeyDown','PreviewKeyDown','MouseDoubleClick','SizeChanged','Unloaded',
               'PreviewMouseDown','MouseDown','LostFocus','GotFocus','Toggled'}
for f in xamls:
    cs = f + '.cs'
    root = trees.get(f)
    if root is None: continue
    xclass = root.getroot().get(X + 'Class')
    if not xclass:
        continue
    if not os.path.exists(cs):
        err(f'{os.path.relpath(f, ROOT)}: thiếu code-behind {os.path.basename(cs)}'); continue
    code = open(cs, encoding='utf-8').read()
    ns, _, cls = xclass.rpartition('.')
    if not re.search(r'namespace\s+' + re.escape(ns) + r'\b', code): err(f'{os.path.basename(cs)}: sai namespace (cần {ns})')
    if not re.search(r'partial\s+class\s+' + re.escape(cls) + r'\b', code): err(f'{os.path.basename(cs)}: thiếu partial class {cls}')
    names = set()
    for el in root.getroot().iter():
        n = el.get(X + 'Name')
        if n:
            if n in names: err(f'{os.path.basename(f)}: trùng x:Name {n}')
            names.add(n)
        for a, v in el.attrib.items():
            if a in event_attrs:
                if not re.search(r'\b' + re.escape(v) + r'\s*\(', code):
                    err(f'{os.path.basename(f)}: thiếu trình xử lý sự kiện {v} ({a})')
    # tên điều khiển dùng trong code phải có trong XAML
    used = set(re.findall(r'\b((?:txt|btn|lbl|cmb|chk|mnu|bar|tab|exp)[A-Z][A-Za-z0-9_]*|resultPanel|paramHost)\b', code))
    for n in sorted(used):
        if n not in names and not n.startswith(('Mnu','Btn','Txt','Cmb')):
            # có thể là biến cục bộ: chỉ cảnh báo khi tên xuất hiện như thành viên điều khiển
            err(f'{os.path.basename(cs)}: dùng tên "{n}" không có x:Name trong XAML')
    for n in sorted(names):
        if not re.search(r'\b' + re.escape(n) + r'\b', code) and not f.endswith('App.xaml'):
            print(f'  (ghi chú) {os.path.basename(f)}: x:Name "{n}" không được dùng trong code-behind')

# --- csproj tồn tại, Page mặc định
if not os.path.exists(os.path.join(ROOT, 'OjGen.App.csproj')): err('thiếu OjGen.App.csproj')

if errors:
    print('PHÁT HIỆN LỖI:')
    for e in errors: print('  -', e)
    sys.exit(1)
print(f'OK: {len(xamls)} file XAML hợp lệ; tên điều khiển, sự kiện, tài nguyên và theme đều khớp.')
