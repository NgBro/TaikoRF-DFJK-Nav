# DFJK Nav — drum-key menu navigation for Taiko no Tatsujin: Rhythm Festival (PC)

Play the whole game from the four drum keys. Menus, song select, settings, the pause menu and
results all work with **D F J K**. Songs are unchanged: during play the keys are drum hits as always.

| Key | In menus |
|---|---|
| **D** | previous item (hold to keep moving) |
| **K** | next item (hold to keep moving) |
| **J** | confirm |
| **F** | back |

## How it works on screen

- Every menu is a ring: the screen's own items, then a row of extra slots shown at the bottom left
  (on a dark strip over the game's button guide).
- The first extra slot is always **◀ Back**. The others are the actions from the game's own guide for
  that screen, e.g. *Help*, *News*, *Playlist*, *Retry*, *Play Options*.
- Going past the last item (K), or before the first (D), puts focus on the extra slots, which turn
  yellow. Press J to use one. F goes back from anywhere, so you never need to scroll to the Back slot.
- **Song select:** the extras sit where the song list wraps around: **Back, Playlist, Genre, ◀ Tab,
  Tab ▶**. Hold D or K to scroll at the game's own fast speed. Scrolling stops at the edge of the list,
  so the extras are easy to land on. *Genre* moves focus to the genre column. Pick a genre with J.
  F on the song list does what the game's own back does there: the first press moves to the genre
  column, and a second press leaves song select.
- **Settings with values** (volume sliders, play options, calibration): J on the item starts
  editing, D/K lower or raise the value, and J or F finishes. The page's own confirm (e.g. *Confirm and
  Close*) is an extra slot.
- **Screens with nothing to select** (e.g. the Rewards Map) get **◀ ▶** or **▲ ▼** slots, which scroll
  the screen when you press J on them.
- **During a song** the mod does nothing. Once paused (Esc) or once the song ends, the pause menu,
  rewards and results screens work with D/F/J/K.

## Install

1. Install **BepInEx 6 bleeding-edge, Unity IL2CPP x64** (tested with `6.0.0-be.788`) into the game
   folder (`...\steamapps\common\Taiko no Tatsujin Rhythm Festival`). Get it from
   <https://builds.bepinex.dev/projects/bepinex_be>.
2. Start the game once and wait for the title screen; the first start takes a while.
3. Copy `DfjkNav.dll` into `BepInEx\plugins\DfjkNav\`.

To build it yourself instead (needs the .NET SDK, and step 2 done first so `BepInEx\interop` exists):

```
dotnet build src -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Taiko no Tatsujin Rhythm Festival"
```

That copies the dll into the game's plugin folder. Use `-p:DeployToGame=false` to only build.

## Configure

Edit `BepInEx\config\dfjk.nav.cfg` (created on first start):

- `[General] Enabled`: turns the mod off without uninstalling it.
- `[Keys] Previous / Next / Confirm / Back`: Unity Input System key names (defaults D, K, J, F).
  Swap `Confirm` and `Back` to confirm with F instead. Change them if your drum controller sends
  other keys.
- `[Debug] Probe`: logs navigation decisions to `BepInEx\LogOutput.log`. Leave it off for normal play.

## Uninstall

- **This mod only:** delete `BepInEx\plugins\DfjkNav\`.
- **BepInEx too:** delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt`,
  `BepInEx\` and `dotnet\` from the game folder.

## Limitations

- **Opening the pause menu mid-song still needs Esc.** During a song D/F/J/K are drum hits.
- **Typing (song search by name, player name) needs a keyboard.**
- **The hidden Ura difficulty can't be revealed:** it's unlocked with extra presses the ring doesn't
  produce.
- **Online modes were not tested.** Treat any mod as a risk on online servers; consider setting
  `Enabled = false` before playing online.
- **Which settings use edit mode is decided from a list of the game's option menus.** A new option
  screen in a game update may need adding (`SelectorAccess.EditableNames`).
- **Tested on game version 3.2.0** (Steam build 23293963), on the title, main menu, Thunder Shrine,
  controller popup, song select (songs, genres, tabs), difficulty select, pause, rewards, results,
  My Room, Outfits, Game Settings and Volume. Other menus use the same building blocks but haven't
  been checked one by one.

## Notes for developers

- **Input:** almost every menu reads input through `ControllerManager` (`GetOkDown`, `GetCancelDown`,
  `GetDirection*`, `GetButtonX/Y/LB/RBDown`, `GetMenuDown`). The mod postfixes those with a
  per-frame intent, and blanks the game's own meanings of D/F/J/K in menus. For example, D is "Right"
  in song select.
- **Cursors:** menus are `MenuSelector<T>`, `InfiniteScroller<T>` or `ScrollableGridSelector<T>` models
  behind `UiView<T>` views. They are read through il2cpp reflection (`SelectorAccess`).
- **Moving the cursor:** it moves with native direction steps. Each step waits until the game's index
  changes and its scroll animation ends. Only free-form menus like the main menu get a direct
  `Select()`.
- **Extra slots:** these come from the visible `UiInputGuide` (groups A/B/X/Y/Menu).
- **Song detection:** `EnsoGameManager.state` and `UiPauseMenu` decide when a song is being played.
- **Gameplay drums:** these are read by `EnsoInput.UpdateController`, which the mod never touches.
