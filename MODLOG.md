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

## Decisions (user)
- BepInEx install OK; agent may drive the game; offline only.
- Song select: extra slots at the list's wrap point (Back, Playlist, Genre, tabs); hold D/K = fast scroll.
- Value items: F/J enters edit mode, D/K adjust, F/J done.

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

## 2026-10-11 — feature complete for the main flows
Verified in game (v3.2.0): Title, MainMenu, Rewards Map, player count, Thunder Shrine, controller-type popup,
SongSelect (songs, hold-scroll, Genre column, store tabs), difficulty select, Enso pause menu, rewards dialog,
results (extras = Back to Start / Omiko City / Retry / Improvement Support), My Room, Outfits (4x7 grid + tabs),
Game Settings, Volume edit mode (changed and restored; left via Cancel). Drum hits during songs still reach
`EnsoInput.UpdateController` (F DonL, J DonR, D KatsuL, K KatsuR).

### Engine facts learned
- Families: MenuSelector<T> (view UiMenuSelector`3), InfiniteScroller<T> (UiInfiniteScroller`3: songs,
  genres), ScrollableGridSelector<T> (UiScrollableGridSelector`3: settings list, shop). Customize menus and
  dialogs (ConfirmationWindow, ...) are MenuSelector subclasses.
- Scrollers expose `DirectionToMove(Dir)` = relative move (songs: Down +1, Up -1, Right +4, Left -4).
- `Select(int)` on a scroller changes the model but not the view: never use it there. MenuSelector.Select(T)
  renders correctly on the free-form main menu; grid MenuSelectors (player count) ignore it visually.
- Selectors drop input while animating: wait for index change, then `SongScroller.IsAnimating` /
  `UiSongScroller.IsScrolling` false. Native hold-scroll = report the direction as held every frame
  (+ `IsRepeatFast` true); GetDirectionRepeat only fires once on a held key.
- Some selectors never set Opened (ThunderShrineSelector): pick by Focus, tie-break Opened then Count
  (song select keeps ThemeSelector (3 tabs) focused next to the song list).
- PlayerNumberSelector keeps 3P/4P in ItemList: reachability = view `Buttons[i].activeInHierarchy`.
- Vanilla keyboard: D = Right in song select (WASD); D/F/J/K otherwise unused in menus.
- Genre column: `SongSelectThunderShrineSceneObjects.uiController.SetFocus(Focuses.Filters)`.
- Controller-type popup: `UiControllerTypeSelector` (IsOpen, buttons[0].isTatacon) — Right/Left toggles.
- Gameplay gate: `EnsoGameManager.state` (Exec < ToResult) + `UiPauseMenu.isActiveAndEnabled`.
- The game was never windowed: its own display setting wins over `-screen-*` args; registry untouched
  (backup in backups/taiko-registry.reg anyway).

### Side effects on the user's save from testing
Played MASEIJIN (Easy) 2x without hitting (fails; +coins/points), song select genre left on POP,
tab Library. Volume unchanged (edited, then Cancel).

## 2026-10-11 — dedicated Back key (user request)
Large lists (400+ songs) made the wrap-point Back slow to reach. Keys now: J = confirm, F = back
(config `[Keys] Confirm` / `Back`; old `Confirm2` removed, user cfg migrated by hand). F sends Cancel from
anywhere, finishes edit mode while editing, and clears pending cursor steps. Verified: song list F ->
genre column (native cancel behaviour), F again -> Thunder Shrine. Edit-mode F not re-tested in game.

## 2026-10-11 — difficulty select as a line (user request)
`DiffSelectBase` subclasses (1P/2P/Network/Training/NinjaDojo/War) use `ICursor.Linear`: extras left of the
first shown item (nearest first, stop at Back, no wrap), items moved with native Left/Right only, and K at
the right end keeps sending Right (Ura switch: `DiffSelectBase.HasUra/IsUra/CanSwitchUra/OnUraSwitch`).
Verified: K Easy->Extreme, extra K = Right passes through; D Extreme->Easy->Play Options->Control Type->Back
(stops), K back to items. Ura toggle confirmed working by the user in game (repeated K on Extreme). Song with Ura in this library: found at song-list step 6 (Taiko Music Pass tab).

## 2026-10-11 — no tap queue + double-tap page skip (user request)
- Taps arriving while the cursor travels (steps pending/awaiting ack/expected index, or selector IsBusy)
  are dropped. Hold-repeat was already gated on !Busy.
- Double tap (same key within 0.2 s) = native Right/Left, only where the cursor's own Right moves > 1 item
  (`DirectionDelta(Right)`, song list = 4). There a single tap is held back 0.2 s; elsewhere instant.
- Seen working in the user's own presses (log: "tap dropped", "page skip Left/Right"); my scripted
  6-tap test overlapped with the user playing, so no clean count.
