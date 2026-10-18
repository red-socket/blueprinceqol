# Blue Prince QoL

![Blue Prince QoL](docs/images/header.jpg)

Quality-of-life tweaks for **Blue Prince**, as a [MelonLoader](https://github.com/LavaGang/MelonLoader) mod.

Most of the mod removes waiting: shorter animations, fades and lock-outs that you sit through dozens of times a day. It doesn't change any game rules. Every feature can be switched off on its own.

> Unofficial fan mod. Not affiliated with or endorsed by Dogubomb or Raw Fury. Back up your save before installing any mod.

## Features

| Feature | What it does |
|---|---|
| Mods menu | Settings → MODS, in both the main menu and the pause menu, lists the mod's options on eight pages, with checkboxes for features and sliders for speeds, in the game's own style. Changes are saved right away. |
| Quit without saving | A QUIT WITHOUT SAVING option under SAVE & QUIT in the pause menu. After a confirmation it returns to the main menu without saving, so Continue starts the same day again. Everything from that day is lost, including permanent unlocks. Steam achievements stay unlocked. |
| HUD | Shows the in-game day, calendar date and time at the top of the screen, in the game's own font with sun and clock icons. It matches the wall clocks. F8 shows or hides it. |
| Run stats *(hidden by default)* | A second line with today's run: rooms drafted, rank, rooms by colour, dead ends, digs, items, unexplored doors, and a low-steps warning. F10 shows or hides it. |
| Boot skip | The Dogubomb and Raw Fury logos are skipped at launch. |
| Intro skip | No camera glide at the start of each day; you have control at the gate straight away. |
| Pickup skip | Item pickup animations and the "You Found …" / "You Bought …" cards are skipped. Items are granted as normal. F9 is an emergency unfreeze in case you ever get stuck. Cards you keep (such as "You Dug Up …") slide off the screen 4× faster when you click them. |
| Terminals | Instant text and shorter pauses. Arrows/W/S and Enter navigate, keys 1 to 9 pick an option, and Esc/Backspace go back. |
| Camera zoom | The camera zooms onto terminals, wall safes, the Watch Tower letter lock, the Dining Room plate and the Utility Closet fuse box 4× faster, in and out. When you open a safe or pull the Drawing Room candlestick, the door and panel animations are 4× faster too. |
| Door keys | The key animation on locked doors is 4× faster. The key is still spent exactly once. |
| Secret Garden | The crank sequence in the Secret Garden runs 4× faster: the gear, the camera fades, the arrow and the niche. The arrow and niche positions are saved as normal. |
| Elevators | Rides in the Boiler Room, Chamber of Mirrors, Foundation and cliff elevators are 3× faster. The whole game runs faster for the length of the ride, so everything stays in sync. Elevator sounds play at normal speed. |
| Rotunda | The Rotunda turns 4× faster, and the gears turn with it. The turning sound fades out as the room stops. |
| Workshop | The black screen while an item is assembled at the Workshop table is 4× shorter, and the assembly sound fades out with it. |
| Office safe | The bust in the Office that rises to reveal the safe moves 4× faster, and its rumble ends with it. |
| Security room scene *(off by default)* | Skips the scene the first time you enter the Security room: the room behaves like one you've already visited, with no fade, no move to the middle of the room and no video. Turn it on in MODS → Cutscenes. |
| Dartboard equation *(off by default)* | While you look at the Billiard Room dartboard, the puzzle is written out as an equation at the bottom of the screen, such as +4 - 1 × 2, with each sign in the colour of its sector. Read it left to right: that's the inner ring first, then outwards. Bullseye shapes and marks on the number ring are written out too, as in (+18 - 16)². The odd numbers around the board are tinted red. Turn it on in MODS → Display. |
| End of day | The end-of-day fades and diary reveal are 4× faster. The Save & Continue screens still wait for you. |
| Dare game over | In Dare mode, the Main Menu button on the Game Over screen shows up after 3 seconds instead of 10, and the fades around it are shorter. Press Space, Enter, Esc or click during the poem to skip ahead to the Game Over screen. |
| Digging | Shorter fade to black when digging. |
| Stairs warps | Shorter black screen for the game's *Stairs Skip* option (turn it on under Settings → Motion Assist). |
| Click unlock | Removes the ~1 s window after pickups, menus and doors where you can walk but can't click, and the short one after leaving a safe or terminal. |
| Cursor guard | Keeps the crosshair visible while an object animates and you can still move. In vanilla it disappears, for example while a Parlor box's key turns. It also shows the mouse pointer as soon as the main menu loads. |
| Move speed | Walking and running are 1.2× faster. Slowdowns, drafting and freezes still work, and step costs don't change. |
| Music volume | Adds a Music slider under the game's volume control (Settings → Volume). F11 mutes music. Sound effects and ambience aren't affected. |

## Screenshots

![The Display page of the Mods menu, with a description of the selected option on the right](docs/images/mods-menu.jpg)

![The in-game day, date and time on the HUD in the Entrance Hall](docs/images/hud.jpg)

![The optional run stats line under the clock](docs/images/run-stats.jpg)

![A Music slider under Master in Settings > Volume](docs/images/music.jpg)

![Numbered options and instant text on a Security terminal](docs/images/terminal.jpg)

## Installation

Pick one of these three ways.

### With r2modman

1. Install [r2modman](https://thunderstore.io/c/blue-prince/p/ebkr/r2modman/) or the Thunderstore App and pick Blue Prince.
2. Download Blue Prince QoL. r2modman installs MelonLoader along with it.
3. Click Start modded. The first launch takes a minute or two while MelonLoader sets itself up.

### All-in-one download

Use this if MelonLoader isn't installed yet.

1. Download `BluePrinceQoL-<version>-with-MelonLoader.zip` from the [Releases](../../releases) page.
2. Extract it into the game folder, the one with `BLUE PRINCE.exe`. In Steam, right-click Blue Prince and choose Manage → Browse local files.
3. Start the game. The first launch takes a minute or two while MelonLoader sets itself up.

The zip includes [MelonLoader](https://github.com/LavaGang/MelonLoader) 0.7.3 (Apache 2.0 license), with its console window and start screen turned off.

### Mod only

If MelonLoader 0.7.3 or later is already installed, download `BluePrinceQoL-<version>.zip` from the [Releases](../../releases) page and extract it into the game folder. It only contains `Mods/BluePrinceQoL.dll`.

To install MelonLoader yourself, run the [MelonLoader installer](https://github.com/LavaGang/MelonLoader/releases), point it at `BLUE PRINCE.exe`, then start the game once.

### Linux / Steam Deck (Proton)

Set the game's Steam launch options to:

```
WINEDLLOVERRIDES="winhttp,version=n,b" %command% --melonloader.hideconsole --melonloader.disablestartscreen
```

The first part makes Proton load MelonLoader. The two options at the end turn off MelonLoader's console window and start screen, which can freeze the game while it loads under Proton. Use the same launch options whichever way you installed.

To check that the mod loaded, look for Blue Prince QoL in `MelonLoader/Latest.log`.

## Configuration

Settings are in `UserData/MelonPreferences.cfg`, under `[BluePrinceQoL]`. The file is created on the first launch. Most of them can also be changed in game under Settings → MODS, in the main menu or the pause menu. Each feature has an `<Feature>_Enabled` switch, plus:

| Setting | Default | Meaning |
|---|---|---|
| `Hud_ToggleKey` | `F8` | Show or hide the HUD. Shift + this key logs diagnostics. |
| `Hud_Font` | `Room Text` | `Room Text` copies the font of the room name on the game's HUD. You can also use a game font name (e.g. `VersicaSerif SDF`), or `IMGUI` for plain text. |
| `Hud_PosX` / `Hud_PosY` / `Hud_Align` | `0.5` / `0.012` / `Center` | Position as a fraction of the screen, and alignment. |
| `Hud_FontScale` / `Hud_IconScale` | `0.03` / `1.15` | Text size (fraction of screen height); icon size relative to the text. |
| `Hud_Use24Hour` / `Hud_ShowDate` / `Hud_ShowIcons` | `false` / `true` / `true` | |
| `RunStats_ToggleKey` / `RunStats_Visible` | `F10` / `false` | |
| `RunStats_Show*` | various | Turn each stat on or off (rooms, rank, colours, dead ends, digs, items, doors, steps…). |
| `RunStats_LowStepsThreshold` | `10` | Show steps in red at or below this. |
| `BootSkip_MenuFadeIn` | `0.5` | Main menu fade-in time in seconds (vanilla: 4). |
| `BootSkip_HideLogos` | `true` | Skip the logos completely (off: they flash by). |
| `PickupSkip_SkipFoundCards` / `_SkipShopCards` / `_SkipDigShopCards` | `true` / `true` / `false` | Which cards to skip (*DigShop* = "You Dug Up" cards). |
| `PickupSkip_UnfreezeKey` | `F9` | Emergency unfreeze. |
| `PickupSkip_CardExitSpeed` | `4` | How much faster a card you click away slides off the screen (1 = vanilla). |
| `Terminal_InstantText` / `_FastWaits` / `_MaxPause` | `true` / `true` / `0.5` | |
| `Terminal_NumberKeys` / `_ShowNumbers` / `_BackKeys` / `_EscapeClosesTerminal` | `true` | Keyboard controls. |
| `CameraZoom_Speed` | `4` | Zoom speed multiplier (1 = vanilla, 0 = instant). |
| `CameraZoom_Terminals` / `_DiningRoom` / `_UtilityPanel` / `_SafesAndLocks` / `_DrawingRoomSafe` / `_EatFade` | `true` | What to speed up. |
| `CameraZoom_OtherObjects` | `false` | Also speed up the same zoom on dartboards, the Room 46 map, shrines and similar objects. |
| `DoorKeySpeed_Speed` | `4` | Key animation speed (1 = vanilla). |
| `DoorKeySpeed_Hotkeys` / `_UseKey` / `_ExitKey` | `false` / `Return` / `Backspace` | Keyboard USE/EXIT on the lock prompt. |
| `SecretGarden_Speed` | `4` | Crank sequence speed (1 = vanilla, at most 10). |
| `ElevatorSpeed_Speed` | `3` | Elevator ride speed (1 = vanilla, at most 6). |
| `ElevatorSpeed_BoilerRoom` / `_ChamberofMirrors` / `_Foundation` / `_Cliff` | `true` | Which elevators to speed up. |
| `Rotunda_Speed` | `4` | Turn speed (1 = vanilla 10 s, at most 10). |
| `Rotunda_FadeSound` / `_SoundFadeTime` | `true` / `0.6` | Fade the turning sound out as the room stops; the fade length in seconds. |
| `Workshop_Speed` | `4` | Speed of the black screen while assembling (1 = vanilla 6 s, at most 10). |
| `OfficeCompartment_Speed` | `4` | Speed of the Office bust rising (1 = vanilla 8 s, at most 10). |
| `SecurityVideo_Enabled` | `false` | Skip the first Security room scene (story content, so off by default). |
| `DartsEquation_Enabled` | `false` | Dartboard equation and red odd numbers. |
| `DartsEquation_RedOddNumbers` | `true` | Tint the odd numbers around the board while the equation is on. |
| `DartsEquation_FontScale` / `DartsEquation_PosY` | `0.045` / `0.935` | Text size, and vertical position as a fraction of the screen from the top. |
| `DartsEquation_Font` | `TypeNeat SDF` | Game font for the equation. Fonts without × and ÷ fall back to x and /. |
| `EndOfDay_Speed` / `_StatsAllAtOnce` / `_MorningFade` | `4` / `false` / `true` | |
| `DareGameOver_MenuDelay` | `3` | Seconds on the Game Over screen before the Main Menu button appears (vanilla: 10). |
| `DareGameOver_SkipKeys` | `Space,Return,Escape,Mouse0` | Keys that skip the poem. Comma-separated Unity key names; `Mouse0` is left click. Leave empty to turn skipping off. |
| `DareGameOver_AutoSkip` | `false` | Always skip the poem, without a key press. |
| `DigSpeed_Speed` / `_XMarksTheSpot` | `4` / `false` | |
| `StairsWarp_Speed` / `_FadeIn` | `4` / `true` | |
| `ClickUnlock_Delay` | `0.1` | Seconds before you can click again after being unfrozen. |
| `ClickUnlock_AfterZooms` | `true` | Also shorten the short lock after leaving a safe, terminal or other zoomed-in object. |
| `CursorGuard_MainMenu` | `true` | Show the mouse pointer as soon as the main menu loads. |
| `MoveSpeed_WalkMultiplier` / `_RunMultiplier` | `1.2` / `1.2` | Allowed range: 0.5 to 3. |
| `MusicVolume_Volume` / `_Muted` / `_MuteKey` | `1.0` / `false` / `F11` | Also adjustable from Settings → Volume. |
| `MusicVolume_IncludeCutscenes` | `false` | By default, cutscene videos keep their sound while music is turned down. |

`StateLogger_Enabled` (off) is a debugging aid. It writes every game state change to the MelonLoader log.

## Compatibility

- Built against MelonLoader 0.7.3 and the Steam version of Blue Prince (Unity 6000.0.58).
- [UnityExplorer](https://github.com/yukieiji/UnityExplorer) works alongside it.
- Game updates can rename things the mod relies on. If that happens, the affected feature logs a warning and the game behaves as normal for that part.

## Building from source

Requirements: .NET SDK 6.0 or later, Python 3 with Pillow (`pip install pillow`), and the game with MelonLoader installed and launched once. The build references MelonLoader's generated assemblies in the game folder.

```bash
cd src/BluePrinceQoL
dotnet build -c Release
```

This builds `BluePrinceQoL.dll` and copies it into the game's `Mods` folder. If your game isn't in the default Steam library, pass `-p:GameDir="/path/to/Blue Prince"`. Pass `-p:CopyToGame=false` to build without copying. The HUD icons in `assets/` are converted and embedded at build time by `tools/png2rgba.py`.

## License

Copyright © 2026 Lucas Pospor.

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, version 3. See [LICENSE](LICENSE).
