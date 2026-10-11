# MODLOG — Taiko DFJK menu navigation

Goal: every menu navigable with only D, F, J, K.
- D / K = move focus (previous / next)
- F / J = confirm
- injected on-screen **Back** button, reachable with D/K, confirmed with F/J

## Recon (2026-10-11)
- Game: Taiko no Tatsujin: Rhythm Festival, Steam appid 2288630, buildid 23293963
- Path: `D:\SteamLibrary\steamapps\common\Taiko no Tatsujin Rhythm Festival`
- Engine: Unity 2022.3.20f1, **IL2CPP** (GameAssembly.dll, metadata v29 → 29.1)
- Anti-cheat: none found. Game has online modes (ranked/rooms) → mod should be menu-only and
  recommend disabling for online play.
- Loaders installed: none.
- Save: `<game>\tkdat\<account id>\userData.bin` (+ Steam autocloud). Logs/caches:
  `%USERPROFILE%\AppData\LocalLow\BANDAI NAMCO Entertainment Inc_\Taiko no Tatsujin Rhythm Festival\Player.log`
- Input: Unity **Input System** (`Unity.InputSystem.dll`) wrapped by the game's `ControllerManager`
  (`TaikoSingletonMonoBehaviour<ControllerManager>`, global namespace).

### Community
- Steve-xmh/TnTRFMod (GPL-3.0): BepInEx 6 (> be.697) or MelonLoader 0.7.1; net6.0; references
  `BepInEx/interop/*.dll`. Patches ControllerManager.GetOkDown/GetCancelDown/GetKeyboardKey etc.
  Cloned read-only into scratchpad for reference only (do not copy GPL code).
- cainan-c/TaikoModManager: installs BepInEx; manages TekaTeka song mods.
- asoji/TNTRFDRPC: BepInEx 6, archived 2025-06 citing a Bandai Namco statement (wording not found/verified).

### ControllerManager API (from Il2CppDumper dump of this build)
- `Dir GetDirectionDown(player, Prio prio, bool rightStick=false)` / `(player, Prio, LRType)`
- `Dir GetDirectionButton(...)` same overloads; `Dir GetDirectionRepeat(player[, LRType | out bool])`
- `bool GetDirectionRepeatTrigger(player, Dir[, out bool])`
- `bool GetOkDown(player)`, `GetCancelDown(player)`, `GetMenuDown`, `GetViewDown`, `GetButtonX/Y/LB/RBDown`
- `bool GetKey/GetKeyDown/GetKeyUp(player, Key)`, `GetButtonDown(player, Buttons)`
- `bool GetDonKatsuDown(player, Taiko)` (Taiko: DonL=0, KatsuL=1, DonR=2, KatsuR=3); `SetDonKatsukeyConfig(short[])`
- private `GetKeyboardKey(in Keyboard, Key)`, `GetKeyboardKeyDown`, `IsKeyDown(...)`
- enums: Dir {None,Right,Left,Up,Down}; Prio {None,Horizontal,Vertical,Previous}; LRType {None,Horizontal,Vertical}
- static `IsKeyboardInputBlocked`; `SetKey(Key)`/`SetButton(Buttons)` = script-injected input

## Route
BepInEx 6 bleeding-edge (Unity IL2CPP x64) + Harmony patches on `ControllerManager` (one input chokepoint),
plus a runtime-built uGUI Back button. Reason: community-proven on this game; managed C# against
Il2CppInterop assemblies; no game files modified besides loader drop-in (removable).

## Tools / scratch
- Il2CppDumper 6.7.46 output: scratchpad `dump/` (dump.cs, DummyDll/, script.json) — outside repo.

## Next
- [ ] user OK: install BepInEx into game folder, drive the game for testing
- [ ] backup save + install BepInEx, first launch to generate interop
- [ ] vertical slice: D/K → Dir, F/J → Ok on main menu, log every ControllerManager query per scene

## 2026-10-11 — vertical slice working (main menu + title)
- Installed BepInEx 6.0.0-be.788 (IL2CPP win-x64) into game folder; first run generated 113 interop dlls.
  Uninstall = delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt`, `BepInEx/`, `dotnet/`.
- Backups: `backups/<ts>/tkdat` (save), `backups/taiko-registry.reg` (display settings; game is exclusive FS 2560x1440).
  `-screen-fullscreen 0` launch args are ignored (game applies its own display setting).
- Game version string on title: 3.2.0.
- **Key finding:** ~50 menus use `Scripts.Common.Selector.MenuSelector<T> : UiModel` (model), shown by
  `UiMenuSelector<TSel,TElem,TBtn> : UiView<TSel>` (MonoBehaviour, protected `Model`). Model has
  `ItemList` (ReactiveProperty<List<T>>), `GetCurrentIndex()`, `Select(int)`, `Decision()`, `Cancel()`,
  `Focus`/`Opened` (IReadOnlyReactiveProperty<bool>). Accessed via il2cpp reflection (generic).
  FindObjectsOfType<MonoBehaviour> + base-type walk: ~2 ms on main menu.
- `ControllerManager.SetKey(Key)` does NOT make GetOkDown true. Injection = Harmony postfix on GetOkDown /
  GetCancelDown / GetDirection* fed from a once-per-frame intent (order-independent).
- Vanilla D/F/J/K do nothing on Title / MainMenu.
- Verified in game: Title J → MainMenu; MainMenu K cycles 0..4 (Thunder Shrine, Dondoko, Store, Taiko Land,
  My Room) → Back (yellow overlay); F on Back → GetCancelDown → Title.
- Bottom bar = `InputGuide` model (groups A/B/X/Y/Menu with text + OnInput subject) — candidate for making
  Help/News/etc. reachable later.

### Test loop
scratchpad `restart.sh` (kill by PID, build+deploy, steam -applaunch) → `to_mainmenu.sh` → `um win drive` keys
(D 0x44, F 0x46, J 0x4A, K 0x4B) → `um win shot --scale 0.33` + grep BepInEx/LogOutput.log.
