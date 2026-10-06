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
Your wallet and stats carry over between cities. Your position is restored only when you load the same city with the same seed. Otherwise you start at the city centre.

## Gameplay loop

0. The game starts on the main menu (`ui/main_menu.tscn`), which lists every city found in `data/cities/`. Press **Play** to load one of them at random. The game ships with **Hanoi**, **Da Nang** and **Ho Chi Minh City**. The HUD shows the current city above the district name.
1. The Job Board lists 5 jobs whose pickups are near the player.
2. Accept a job. A pickup marker appears and an arrow around the bike points to it.
3. Drive into the marker and press **E** to collect the package or pick up the passenger.
4. Drive to the drop-off marker and press **E** again.
5. A summary popup appears and the reward goes into your wallet.
6. New jobs are generated around your current position. The board also refreshes when you open it after driving more than 900 px away.

### Job types

| Job | Timed | Notes |
|---|---|---|
| Parcel, Documents, Fragile Electronics | No | Package delivery with no deadline. |
| **Hot Food** | Yes | Short trips. Arriving late cuts the reward by 50%. |
| **Passenger Ride** | Yes | Bike taxi with a named customer. The passenger sits behind the rider. Arriving late cuts the fare by 30%. |

Timed jobs show their limits on the Job Board card (`TIMED Pickup 0:20 - Ride 0:30`) and a countdown in the HUD. The countdown turns orange below 50% of the time and red below 25%.

- **Pickup deadline:** if you miss it, the job **fails**. The customer cancels, you get no pay, and the board reopens with new jobs.
- **Drop-off deadline:** if you miss it, you can still finish the job, but the reward is cut by the template's `LatePenalty` and the HUD shows `LATE +m:ss`. Set `LatePenalty = 1` to make the job fail at this deadline instead.
- **How the limits are calculated:** `limit = BaseTime + route km × TimePerKm`, rounded up to 5 s. The pickup limit is recalculated from where you are when you press Accept.

## Folder structure

```
autoload/      Singletons: EventBus.cs (signals), GameManager.cs (state), SaveManager.cs (persistence)
scenes/        main, player, city_map, job_marker (.tscn)
data/cities/   City definitions, one JSON file per city (see "Adding a city")
ui/            main_menu (startup scene), hud, job_board, job_entry, delivery_result_popup (.tscn)
scripts/
  Main.cs      Composition root: picks a random city, registers the world, loads the save, opens the board
  player/      Player (motorbike controller), BikeVisual, TargetIndicator, VehicleStats
  map/         CityMap (procedural builder), CityLoader + CityConfig (JSON -> CityRegionData), Building, CityRegionData, DistrictData, DeliveryLocation
  jobs/        JobManager (state machine), JobGenerator, JobTemplate, JobData, JobMarker, DeliveryResult
  economy/     Wallet (+ WalletEntry), PlayerStats
  save/        SaveData.cs: JSON save DTOs
  ui/          MainMenu, Hud, JobBoard, JobEntry, DeliveryResultPopup
resources/
  jobs/        JobTemplate .tres files (one per job type): parcel, documents, fragile_electronics, food_delivery, passenger_ride
  vehicles/    VehicleStats .tres files
  ui/          ui_theme.tres
```

All C# code is in the `ShipperSimulator` namespace. Godot requires each script file to have the same name as its class.

## Architecture

- **EventBus** (`EventBus.Instance`) is the only connection between systems. It declares Godot `[Signal]`s. Listeners subscribe with C# event syntax, for example `EventBus.Instance.JobAccepted += OnJobAccepted;`. Senders call `EmitSignal(EventBus.SignalName.X, ...)`. UI scripts emit requests such as `JobAcceptRequested`, and `JobManager` emits results such as `JobStateChanged`, `JobDelivered`, `JobFailed`, `JobTimerUpdated` (every frame of a timed job) and `NavigationTargetChanged`. Nothing in the UI holds a reference to `JobManager`.
  - Subscribe with **methods, not lambdas**, and **unsubscribe in `_ExitTree()`** with the same method (`-=`). The `EventBus` signals are declared in C#, so their C# events are plain delegates and Godot does **not** disconnect them when a node is freed. A handler you forget to remove keeps running after a scene change and throws `ObjectDisposedException`. Built-in Godot signals such as `Button.Pressed` are disconnected automatically.
- **Scene lifecycle:** `Main._ExitTree()` calls `GameManager.UnregisterWorld()`, so autosave and play-time tracking stop when you leave the gameplay scene.
- **GameManager** (`GameManager.Instance`) owns the global state (`Wallet`, `PlayerStats`, world references) and holds the helpers for payouts and formatting (`FormatMoney`, `FormatDistance`...). It also builds the data that goes into the save file.
- **SaveManager** writes and reads versioned JSON through `System.Text.Json`, using the DTOs in `scripts/save/SaveData.cs` with snake_case keys. Add migrations in `Migrate()`.
- **Data resources** (`JobTemplate`, `VehicleStats`) are `[GlobalClass]` resources, so you can create and edit them in the inspector. In `.tres` files their properties use the C# names (PascalCase). `CityRegionData` and `DistrictData` are created at runtime by `CityLoader` from the city JSON files.
- **Classes that never go through Godot** (`Wallet`, `PlayerStats`, `JobGenerator`, `DeliveryLocation`, `CityLoader`, the save and city DTOs) are plain C# classes that use `event Action` instead of Godot signals. `DeliveryResult` is a `RefCounted` because it is passed through a Godot signal.
- **JobManager** runs the job state machine (`Idle → ToPickup → ToDelivery → Idle`), spawns the markers and enforces time limits.
  - Each phase has its own timer (`_phaseTime`, accumulated from `delta`, so it stops if the game is paused).
  - Missing the pickup deadline calls `FailActiveJob()`. Missing the drop-off deadline marks the job late, and `GameManager.CompleteDelivery()` then subtracts `JobData.LatePenalty`.
- **JobTemplate** configures each job type:
  - `Cargo` (`Package` or `Passenger`) changes the texts, the marker icon and the bike visual.
  - `CustomerNames` gives each job a named customer.
  - The **Time limits** group holds `IsTimed`, `PickupBaseTime`, `PickupTimePerKm`, `DeliveryBaseTime`, `DeliveryTimePerKm` and `LatePenalty`.
- **JobGenerator** turns templates and the map's addresses into `JobData`. The reward is:
  `(BaseReward + km × RewardPerKm) × district multiplier × GameManager.GetRewardMultiplier() ± variance`.
- **CityMap** builds the city from a `CityRegionData` resource. Roads, sidewalks, parks and water are drawn in `_Draw()`. Buildings, trees and water blocks are `StaticBody2D` obstacles on physics layer 2. Every side of every block gets one curbside address, so there are `4 × GridSize.X × GridSize.Y` addresses.
  - **Seed:** each city sets `"seed"` in its JSON file (Hanoi 1010, Da Nang 2020, HCMC 3030). A fixed seed always produces the same layout. `0` produces a new random layout on every load. The seed actually used is `CityMap.ActiveSeed`, and it is saved together with `RegionId`.
  - **Block types:** for each block the generator rolls `DistrictData.WaterChance` first, then `ParkChance`, and otherwise fills the block with building lots. A district with `WaterChance = 1` becomes a river, for example `han_river` and `saigon_river`. You can still drive along the roads that cross it.
- **City selection:** `Main._EnterTree()` loads every city with `CityLoader.LoadAll(Main.CitiesFolder)` and assigns a random one to `CityMap.Region` before `CityMap._Ready()` builds the map.

Physics layers: 1 = player, 2 = world, 3 = interactables (markers).

## Adding a city

Create a new `.json` file in `data/cities/`. The game picks it up automatically, with no code or scene changes. The menu lists it and it joins the random pick. Only `code`, `name` and `districts` (each with `code`, `name` and `area`) are required. Every other field has a default. Comments (`//`) and trailing commas are allowed.

```jsonc
{
  "code": "hai_phong",              // unique id, stored in the save file: keep it stable
  "name": "Hai Phong",              // shown on the menu and the HUD
  "seed": 4040,                     // layout seed; 0 = a new random layout every time
  "grid": { "columns": 10, "rows": 8, "block_size": 420, "road_width": 140 },
  "colors": { "road": "#383b42", "lane": "#d9cc73b3", "park": "#4d8c4d", "water": "#336b99" },
  "water_place_name": "Harbour",    // address name for blocks next to water
  "districts": [
    {
      "code": "le_chan", "name": "Le Chan",
      "area": [0, 0, 10, 8],        // [x, y, width, height] in blocks
      "sidewalk_color": "#8c8c8c",
      "building_colors": ["#7a8a9c", "#9c8a7a"],
      "park_chance": 0.1,           // rolled after water_chance
      "water_chance": 0.0,          // 1.0 = the whole district is water (a river)
      "min_lots": 2, "max_lots": 3, // buildings per row of a block (1-6)
      "reward_multiplier": 1.0
    }
  ],
  "streets": {
    "horizontal": ["..."],          // rows + 1 names, top to bottom (shorter lists repeat)
    "vertical": ["..."]             // columns + 1 names, left to right
  }
}
```

`CityLoader` validates every file. A file with broken JSON, a missing `code`/`name`, a district outside the grid or a duplicate `code` is **skipped**, and the reason appears in Godot's Output/Debugger panel. Mistakes that still produce a playable city, such as blocks not covered by any district or the wrong number of street names, only cause a warning. The Windows export preset includes `data/cities/*.json` through `include_filter`. Add the same filter to any new export preset, otherwise the exported game will have no cities.

## Where future features plug in

| Feature | Extension point |
|---|---|
| Fuel | Add `FuelCapacity` and `Consumption` to `VehicleStats`. Add a `FuelSystem` node that reads the player's speed and calls `Wallet.Spend()` at gas stations (a new location type in `CityMap`). |
| Weather | Add a `WeatherManager` autoload. Have it adjust `VehicleStats.Grip` and return a bonus from `GameManager.GetRewardMultiplier()`. |
| Traffic | Add `scenes/traffic_vehicle.tscn` on physics layer 2. Use `CityMap.GetIntersection()` and the road grid for paths. |
| Reputation | Add a stat to `PlayerStats` and `StatsSaveData`, and adjust it in `GameManager.CompleteDelivery()`. Expose it through `GetRewardMultiplier()` and add fields to `DeliveryResult`. |
| Vehicle upgrades | Add more `VehicleStats` .tres files, buy them with `Wallet.Spend()`, and swap `Player.VehicleStats`. Save the owned vehicle id in the save DTOs. |
| More cities | Add a JSON file to `data/cities/` (see "Adding a city"). A city picker on the menu could pass the chosen `code` to `Main` in place of the random pick in `Main._EnterTree()`. |
| Modded cities | Call `CityLoader.LoadAll("user://cities")` as well, so players can add JSON files without rebuilding the game. |
| More timed jobs | Set `IsTimed = true` on any `JobTemplate` .tres. Time limits and penalties need no code. |
| Speed bonus / tips | `DeliveryResult.DeliveryTime` and `JobData.DeliveryTimeLimit` are available. Add a bonus in `GameManager.CompleteDelivery()` for jobs finished well before the deadline. |
| Failure stats / reputation | Listen to `EventBus.JobFailed`, then count failures in `PlayerStats` (and `StatsSaveData`). |
| Saving the active job | `JobData` only holds plain data. Add a DTO for it to `GameSaveData`. |
