# Shipper Simulator

A 2D top-down delivery game (MVP prototype) for **Godot .NET (C#)**.
It was tested with Godot 4.7.2 .NET and uses no APIs added after 4.4.

## Requirements and building

- Use the **.NET build** of Godot (`Godot_v4.x-stable_mono`). The standard build cannot run C# scripts.
- You need the .NET 8 SDK or newer. The project targets `net8.0`.
- `ShipperSimulator.csproj` uses `Godot.NET.Sdk/4.7.2`. If you use a different Godot version, change that SDK version to match your editor.
- Open the project in the editor and press **Build** (hammer icon) or **F5**. To build from the command line:

  ```bash
  dotnet build ShipperSimulator.csproj
  ```

  If the restore cannot find the Godot packages, point it at the editor's bundled NuGet folder:
  `-p:RestoreAdditionalProjectSources=<GodotDir>/GodotSharp/Tools/nupkgs`.

## Controls

| Key | Action |
|---|---|
| W / ↑ | Throttle |
| S / ↓ | Brake, then reverse |
| A D / ← → | Steer (you need some speed to turn) |
| Space | Handbrake (lets the bike drift) |
| E | Pick up or deliver when inside a marker; also closes the result popup |
| Tab / J | Open or close the Job Board |
| F5 | Quick save |

The game autosaves after every delivery, every 60 s, and when you close the window.
The save file is `user://savegame.json`. Delete it to reset your progress.

## Gameplay loop

1. The Job Board lists 5 jobs whose pickups are near the player.
2. Accept a job. A pickup marker appears and an arrow around the bike points to it.
3. Drive into the marker and press **E** to collect the package.
4. Drive to the drop-off marker and press **E** again.
5. A summary popup appears and the reward goes into your wallet.
6. New jobs are generated around your current position. The board also refreshes when you open it after driving more than 900 px away.

## Folder structure

```
autoload/      Singletons: EventBus.cs (signals), GameManager.cs (state), SaveManager.cs (persistence)
scenes/        main, player, city_map, job_marker (.tscn)
ui/            hud, job_board, job_entry, delivery_result_popup (.tscn)
scripts/
  Main.cs      Composition root: registers the world, loads the save, opens the board
  player/      Player (motorbike controller), BikeVisual, TargetIndicator, VehicleStats
  map/         CityMap (procedural builder), Building, CityRegionData, DistrictData, DeliveryLocation
  jobs/        JobManager (state machine), JobGenerator, JobTemplate, JobData, JobMarker, DeliveryResult
  economy/     Wallet (+ WalletEntry), PlayerStats
  save/        SaveData.cs: JSON save DTOs
  ui/          Hud, JobBoard, JobEntry, DeliveryResultPopup
resources/
  jobs/        JobTemplate .tres files (one per job type)
  districts/   DistrictData .tres files
  regions/     CityRegionData .tres files (city layout, districts, street names)
  vehicles/    VehicleStats .tres files
  ui/          ui_theme.tres
```

All C# code is in the `ShipperSimulator` namespace. Godot requires each script file to have the same name as its class.

## Architecture

- **EventBus** (`EventBus.Instance`) is the only connection between systems. It declares Godot `[Signal]`s. Listeners subscribe with C# event syntax, for example `EventBus.Instance.JobAccepted += OnJobAccepted;`. Senders call `EmitSignal(EventBus.SignalName.X, ...)`. UI scripts emit requests such as `JobAcceptRequested`, and `JobManager` emits results such as `JobStateChanged`, `JobDelivered` and `NavigationTargetChanged`. Nothing in the UI holds a reference to `JobManager`.
  - Subscribe with **methods, not lambdas**. Godot automatically disconnects a method of a node when that node is freed, but it cannot do that for a lambda.
- **GameManager** (`GameManager.Instance`) owns the global state (`Wallet`, `PlayerStats`, world references) and holds the helpers for payouts and formatting (`FormatMoney`, `FormatDistance`...). It also builds the data that goes into the save file.
- **SaveManager** writes and reads versioned JSON through `System.Text.Json`, using the DTOs in `scripts/save/SaveData.cs` with snake_case keys. Add migrations in `Migrate()`.
- **Data resources** (`JobTemplate`, `DistrictData`, `CityRegionData`, `VehicleStats`) are `[GlobalClass]` resources, so you can create and edit them in the inspector. In `.tres` files their properties use the C# names (PascalCase).
- **Classes that never go through Godot** (`Wallet`, `PlayerStats`, `JobGenerator`, `DeliveryLocation`, the save DTOs) are plain C# classes that use `event Action` instead of Godot signals. `DeliveryResult` is a `RefCounted` because it is passed through a Godot signal.
- **JobManager** runs the job state machine (`Idle → ToPickup → ToDelivery → Idle`) and spawns the markers.
- **JobGenerator** turns templates and the map's addresses into `JobData`. The reward is:
  `(BaseReward + km × RewardPerKm) × district multiplier × GameManager.GetRewardMultiplier() ± variance`.
- **CityMap** builds the city from a `CityRegionData` resource using a fixed seed, so saved positions stay valid. Roads and sidewalks are drawn in `_Draw()`. Buildings and trees are `StaticBody2D` obstacles on physics layer 2. Every side of every block gets one curbside address (320 addresses in total).

Physics layers: 1 = player, 2 = world, 3 = interactables (markers).

## Where future features plug in

| Feature | Extension point |
|---|---|
| Fuel | Add `FuelCapacity` and `Consumption` to `VehicleStats`. Add a `FuelSystem` node that reads the player's speed and calls `Wallet.Spend()` at gas stations (a new location type in `CityMap`). |
| Weather | Add a `WeatherManager` autoload. Have it adjust `VehicleStats.Grip` and return a bonus from `GameManager.GetRewardMultiplier()`. |
| Traffic | Add `scenes/traffic_vehicle.tscn` on physics layer 2. Use `CityMap.GetIntersection()` and the road grid for paths. |
| Reputation | Add a stat to `PlayerStats` and `StatsSaveData`, and adjust it in `GameManager.CompleteDelivery()`. Expose it through `GetRewardMultiplier()` and add fields to `DeliveryResult`. |
| Vehicle upgrades | Add more `VehicleStats` .tres files, buy them with `Wallet.Spend()`, and swap `Player.VehicleStats`. Save the owned vehicle id in the save DTOs. |
| Multiple regions | Add more `CityRegionData` .tres files. Set `CityMap.Region` and call `Build()`, and save `RegionId`. |
| Timed / bonus jobs | Add properties to `JobTemplate` and `JobData`. `ElapsedTime` is already tracked in `DeliveryResult`. |
| Saving the active job | `JobData` only holds plain data. Add a DTO for it to `GameSaveData`. |
