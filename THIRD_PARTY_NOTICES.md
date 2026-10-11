# Sources

- C# layout and gameplay adapters: ZeusAFK/LibreKO-knightonline-ui-chaos, AGPL-3.0.
  https://github.com/ZeusAFK/LibreKO-knightonline-ui-chaos
- DXT reader: ZeusAFK/LibreKO-Assets, AGPL-3.0; copied `bakers/dxt_to_png.py`.
  https://github.com/ZeusAFK/LibreKO-Assets
- UIF format reference: Open-KO/KnightOnline, `src/N3Base/N3UI*.cpp`.
  https://github.com/Open-KO/KnightOnline
- UI artwork/layout input: user's local Knight Online installation. Original game artwork retains
  its original ownership; the code license does not relicense these game assets.
- Mail envelope artwork: existing `main_ui_01.png` from ZeusAFK/LibreKO-knightonline-ui-chaos,
  copied unchanged as `assets/textures/classic_mail_chaos.png`. The normal image and notification
  frame sequence follow `assets/layouts/co_hpbar_us.json`. Original game artwork retains its ownership.
  https://github.com/ZeusAFK/LibreKO-knightonline-ui-chaos

- 2.x-era window art in `assets/myko`: layouts and textures of a 2.x-era Knight Online client UI
  (UIF window overrides for the Power Up Store, quest NPC menu and viewer, status bar menu,
  notice/premium bars and the notice content box), decoded from loose UIF/DXT files and
  converted to the plugin's layout JSON format with the DXT reader above. `assets/myko/index.json`
  lists each file with its SHA-256. Original game artwork retains its original ownership; the code
  license does not relicense these game assets.

See LICENSE for the adapter code's AGPL-3.0 terms.

- Hunt icon: user-supplied visual, reduced to transparent 32/64-pixel PNGs.
  The two preview fixtures are visual inputs, not captured account/game state.
  The code license does not relicense third-party artwork.
