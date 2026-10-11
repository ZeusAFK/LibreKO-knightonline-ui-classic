"""Copy the 2.x-era window art (converted UIF layouts and their textures) into the plugin.

The plugin bundles this art under `assets/myko/{layouts,textures}`; `src/LocalAssets.cs` reads it from there.
Windows that find their layout, with every texture it references, use it; otherwise they keep the Classic
design. A developer can override the bundled art with an `assets-local/` folder next to the installed plugin
(untracked) or with the KNIGHTONLINE_UI_CLASSIC_LOCAL_ASSETS environment variable.

Usage:
  python -I install_local_assets.py --source <converted dir with layouts/ and textures/> --bundle [layout ...]
      Refresh the bundled copy in this repository (assets/myko), replacing what is there, and write its index.
  python -I install_local_assets.py --source <converted dir> --target <plugin dir> [layout ...]
      Install a developer override into <plugin dir>/assets-local.
Example:
  python -I install_local_assets.py --source D:/libreko/research/myko-client/converted-lilith --bundle
"""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
BUNDLE = REPO / 'assets' / 'myko'
SOURCE = '2.x-era Knight Online client UI (UIF window overrides), converted to the plugin layout JSON format'

DEFAULT_LAYOUTS = ['re_powerupstore']
# Quest NPC talk menu and quest viewer (src/Windows/MykoQuestDialogue.cs).
DEFAULT_LAYOUTS += ['re_quest_npc_menu', 're_quest_viewer']
# HUD: letter plates and clock, notice/premium bars and their content box (which also frames the time box).
DEFAULT_LAYOUTS += ['re_el_hpmenu', 're_ka_hpmenu', 're_eventnotice', 're_el_eventcontent', 're_ka_eventcontent',
                    're_eventpremium']


def textures(node):
    if node.get('texture'):
        yield node['texture']
    for child in node.get('children', []):
        yield from textures(child)


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--source', type=Path, required=True)
    where = ap.add_mutually_exclusive_group(required=True)
    where.add_argument('--target', type=Path, help='plugin folder (assets-local/ is created inside)')
    where.add_argument('--bundle', action='store_true', help=f'refresh the bundled copy in {BUNDLE}')
    ap.add_argument('layouts', nargs='*', default=DEFAULT_LAYOUTS)
    args = ap.parse_args()
    out = BUNDLE if args.bundle else args.target / 'assets-local'
    if args.bundle and out.exists():
        shutil.rmtree(out)
    (out / 'layouts').mkdir(parents=True, exist_ok=True)
    (out / 'textures').mkdir(parents=True, exist_ok=True)
    index = {'source': SOURCE, 'layouts': {}, 'textures': {}}
    for name in args.layouts:
        layout = args.source / 'layouts' / f'{name}.json'
        root = json.loads(layout.read_text(encoding='utf-8'))
        needed = sorted(set(textures(root)))
        missing = [t for t in needed if not (args.source / 'textures' / t).exists()]
        if missing:
            raise SystemExit(f'{name}: missing textures {missing}')
        shutil.copy2(layout, out / 'layouts' / layout.name)
        for texture in needed:
            shutil.copy2(args.source / 'textures' / texture, out / 'textures' / texture)
            index['textures'][texture] = sha256(out / 'textures' / texture)
        index['layouts'][name] = {'file': f'layouts/{layout.name}', 'rect': root.get('rect'),
                                  'sha256': sha256(out / 'layouts' / layout.name), 'textures': needed}
        print(f'{name}: layout + {len(needed)} textures -> {out}')
    if args.bundle:
        index['textures'] = dict(sorted(index['textures'].items()))
        (out / 'index.json').write_text(json.dumps(index, indent=2) + '\n', encoding='utf-8')
        files = [p for p in out.rglob('*') if p.is_file()]
        print(f'bundle: {len(files)} files, {sum(p.stat().st_size for p in files)} bytes')


if __name__ == '__main__':
    main()
