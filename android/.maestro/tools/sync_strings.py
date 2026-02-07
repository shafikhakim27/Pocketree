#!/usr/bin/env python3
import re
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STRINGS_PATH = ROOT / 'app' / 'src' / 'main' / 'res' / 'values' / 'strings.xml'
FLOWS_DIR = ROOT / '.maestro' / 'flows'

def load_strings(path):
    tree = ET.parse(path)
    root = tree.getroot()
    d = {}
    for s in root.findall('string'):
        val = s.text or ''
        norm = ' '.join(val.split())
        d[norm] = val
    return d

def replace_in_file(path, strings_map, stats):
    text = path.read_text(encoding='utf-8')
    changed = False

    # match patterns like: - assertVisible: "..."  or - tapOn: "..."
    pattern = re.compile(r'(-\s*(?:assertVisible|tapOn)\s*:\s*)"([^\"]+)"')

    def repl(m):
        nonlocal changed
        prefix = m.group(1)
        val = m.group(2)
        norm = ' '.join(val.split())
        stats['seen'].append((str(path), val))
        if norm in strings_map:
            target = strings_map[norm]
            if target != val:
                new_val = target.replace('"', '\\"')
                new_val = new_val.replace('\n', '\\n')
                changed = True
                stats['replaced'].append((str(path), val, target))
                return f'{prefix}"{new_val}"'
            else:
                stats['matched_nochange'].append((str(path), val))
                return m.group(0)
        else:
            stats['unmatched'].append((str(path), val))
            return m.group(0)

    new_text = pattern.sub(repl, text)
    if changed:
        path.write_text(new_text, encoding='utf-8')
        stats['updated_files'].append(str(path))

def main():
    if not STRINGS_PATH.exists():
        print('strings.xml not found at', STRINGS_PATH)
        return
    strings_map = load_strings(STRINGS_PATH)

    # update files in .maestro root and flows
    files = [ROOT / '.maestro' / 'flow.yaml']
    if FLOWS_DIR.exists():
        files += list(FLOWS_DIR.glob('*.yaml'))

    stats = {
        'seen': [],
        'replaced': [],
        'matched_nochange': [],
        'unmatched': [],
        'updated_files': []
    }

    for f in files:
        replace_in_file(f, strings_map, stats)

    log_path = Path(__file__).resolve().parent / 'sync_strings.log'
    with log_path.open('w', encoding='utf-8') as fh:
        fh.write('Sync strings run\n')
        fh.write('=================\n')
        fh.write(f"strings.xml: {STRINGS_PATH}\n")
        fh.write(f"Files scanned: {len(files)}\n\n")

        fh.write('Updated files:\n')
        for u in stats['updated_files']:
            fh.write(f'- {u}\n')
        fh.write('\n')

        fh.write('Replacements performed:\n')
        for fpath, old, new in stats['replaced']:
            fh.write(f'- {fpath}: "{old}" => "{new}"\n')
        fh.write('\n')

        fh.write('Matched (no change needed):\n')
        for fpath, v in stats['matched_nochange']:
            fh.write(f'- {fpath}: "{v}"\n')
        fh.write('\n')

        fh.write('Unmatched flow strings (need review):\n')
        for fpath, v in stats['unmatched']:
            fh.write(f'- {fpath}: "{v}"\n')
        fh.write('\n')

    print('Sync complete. Log written to', log_path)


if __name__ == '__main__':
    main()
