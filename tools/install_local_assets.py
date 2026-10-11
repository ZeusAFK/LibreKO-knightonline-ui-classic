"""Copy optional local art into an installed plugin's assets-local/ folder.

The plugin repository ships no art from other clients. Windows that have a local layout (currently the
Power-Up Store, layout `re_powerupstore`) use it when `assets-local/layouts/<name>.json` and every texture it
references exist; otherwise they keep the bundled Classic design.

Usage:
  python -I install_local_assets.py --source <converted dir with layouts/ and textures/> --target <plugin dir> [layout ...]
Example:
  python -I install_local_assets.py --source D:/libreko/research/myko-client/converted-lilith \
      --target D:/libreko/build/client/plugins/knightonline-ui-classic
"""
import argparse
import json
import shutil
from pathlib import Path

DEFAULT_LAYOUTS = ['re_powerupstore']
# Quest NPC talk menu and quest viewer (src/Windows/MykoQuestDialogue.cs).
DEFAULT_LAYOUTS += ['re_quest_npc_menu', 're_quest_viewer']
# HUD: letter plates and clock, event schedule, notice bars and their content box.
DEFAULT_LAYOUTS += ['re_el_hpmenu', 're_ka_hpmenu', 're_el_scheduler', 're_ka_scheduler',
                    're_eventnotice', 're_el_eventcontent', 're_ka_eventcontent', 're_eventpremium', 'co_time']


def textures(node):
    if node.get('texture'):
        yield node['texture']
    for child in node.get('children', []):
        yield from textures(child)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--source', type=Path, required=True)
    ap.add_argument('--target', type=Path, required=True, help='plugin folder (assets-local/ is created inside)')
    ap.add_argument('layouts', nargs='*', default=DEFAULT_LAYOUTS)
    args = ap.parse_args()
    out = args.target / 'assets-local'
    (out / 'layouts').mkdir(parents=True, exist_ok=True)
    (out / 'textures').mkdir(parents=True, exist_ok=True)
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
        print(f'{name}: layout + {len(needed)} textures -> {out}')


if __name__ == '__main__':
    main()
