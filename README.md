# TnTRF-DFJK-Controls

Play the whole game from the four drum keys. Menus, song select, settings, the pause menu and
results all work with **D F J K**. Songs are unchanged: during play the keys are drum hits as always.

| Key | In menus |
|---|---|
| **D** | previous item (hold to keep moving; double tap = page back on the song list) |
| **K** | next item (hold to keep moving; double tap = page forward on the song list) |
| **J** | confirm |
| **F** | back |

## Install

1. Install **BepInEx. Get it from <https://builds.bepinex.dev/projects/bepinex_be>.
2. Start the game once and wait for the title screen.
3. Copy `DfjkNav.dll` into `Taiko no Tatsujin Rhythm Festival\BepInEx\plugins\DfjkNav\`.

## Configure

Edit `BepInEx\config\dfjk.nav.cfg` (created on first start):

- `[General] Enabled`: turns the mod off without uninstalling it.
- `[Keys] Previous / Next / Confirm / Back`: Unity Input System key names (defaults D, K, J, F).
  Swap `Confirm` and `Back` to confirm with F instead. Change them if your drum controller sends
  other keys.
- `[Debug] Probe`: logs navigation decisions to `BepInEx\LogOutput.log`. Leave it off for normal play.

## Uninstall

- Delete `BepInEx\plugins\DfjkNav\`.

## Limitations

- **Opening the pause menu mid-song still needs Esc.** During a song D/F/J/K are drum hits.
- **Typing (song search by name, player name) needs a keyboard.**
- **Online modes were not tested.** Treat any mod as a risk on online servers; consider setting
  `Enabled = false` before playing online.
- **Which settings use edit mode is decided from a list of the game's option menus.** A new option
  screen in a game update may need adding (`SelectorAccess.EditableNames`).
- **Tested on game version 3.2.0** (Steam build 23293963), on the title, main menu, Thunder Shrine,
  controller popup, song select (songs, genres, tabs), difficulty select, pause, rewards, results,
  My Room, Outfits, Game Settings and Volume. Other menus use the same building blocks but haven't
  been checked one by one.
