# SpaceV

A 2.5D rocket game made with Godot 4.7.2 (.NET / C#). Build a rocket, fly it from Earth to orbit, cruise to Mars and land it. Each failed flight earns research points (RP) that unlock better parts.

## Play the exported build (Windows)

1. Open the `export/` folder (or the zip you received).
2. Run `SpaceV.exe`. Keep every file of the folder together (`SpaceV.pck` and the `data_SpaceV_windows_x86_64` folder hold the game data and the C# code).
3. `SpaceV.console.exe` runs the same game with a console window that shows the log, which is useful for bug reports.

No install is needed. Turn off Vietnamese typing (Unikey, EVKey) while playing: it turns repeated A or D into extra keys. Saves go to `%APPDATA%\Godot\app_userdata\SpaceV`.

## Run from source

1. Install Godot 4.7.2 **.NET** and the .NET 8 SDK.
2. Open `project.godot` in Godot, click **Build** (top right), then press **F5**. The main scene is `scenes/main.tscn`.
3. Test scenes live in `tests/a`, `tests/b` and `tests/c`. Open one and press **F6**.

## Export a new build

1. Install the export templates once: **Editor → Manage Export Templates → Download and Install** (the .NET templates for 4.7.2).
2. **Project → Export…**, choose the **Windows Desktop** preset (already set to `export/SpaceV.exe`), then **Export Project**. Leave "Export With Debug" off for a release build.
3. The `export/` folder is ignored by git. Zip it to share.

## How to play

1. **Build**: place parts on the grid, add modules to the pod, then confirm.
2. **Earth ascent**: launch, stage and steer into orbit.
3. **Cruise**: do the transfer burn, then survive about 210 days of hazards on the way to Mars.
4. **Mars landing**: turn the heat shield toward the flow, open the parachute at the right moment (below about Mach 2.5), drop the heat shield, then either fire the landing engine or touch down on an airbag.
5. **Debrief**: earn RP, buy tech nodes in the research screen, and try again. Landing on Mars wins the game.

## Controls

### Flight

| Key | Action |
| --- | --- |
| A / D | Pitch left / right |
| Shift / Ctrl | Throttle up / down |
| Z / X | Full throttle / cut throttle |
| Space | Next stage |
| F | Jettison fairing |
| P | Deploy parachute (too early and it tears, too late and it cannot slow you down) |
| T | SAS on / off |
| 1 / 2 / 3 / 4 | SAS mode: stability assist / hold pitch / prograde / retrograde |
| R | RCS on / off |
| Q / E | RCS left / right |
| G | Deploy or retract panels and antennas |
| C | Switch electronics to auxiliary power |
| V | Seal a leak |
| Esc (hold 1.5 s) | Abort the flight |
| Mouse wheel | Zoom in / out |

### Build screen

| Input | Action |
| --- | --- |
| Left click | Place the selected part |
| Left drag (nothing selected) | Move a placed part |
| Right click | Remove a part |
| Mouse wheel | Zoom |
| Tab | Decoupler mode on / off (then left click a joint to add a decoupler, right click to remove it) |
| Esc | Cancel the selection or the drag |
| Hover a part | Show its tooltip |
| "Wind tunnel" button | Check the drag and stability of the design |

## Team

- Dev A: rocket building (`building/`)
- Dev B: physics and research (`physics/`, `research/`)
- Dev C: control and integration (`control/`, `presentation/`)

Rules for contributors are in `AGENTS.md`, the per-dev guides are in `docs/agents/`, and public API changes are logged in `docs/API_CHANGES.md`.
