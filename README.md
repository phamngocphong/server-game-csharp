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
| Esc / P, or the **II** button at the top left | Pause menu |

### Touch controls (mobile)

On a **mobile build** (Android/iOS) or a device with a touchscreen, on-screen controls appear:

- **Joystick (left half of the screen):** it is floating, so it appears wherever you put your thumb. There are two modes, which you switch in the pause menu:
  - **Direction** (default): point the stick where you want to go. The bike steers towards that direction and accelerates according to how far you push.
  - **Steer:** up = throttle, down = brake/reverse, left/right = steer, like the keyboard.
- **BRAKE:** hold to brake.
- **E:** interact. It glows when there is something to pick up, deliver or refuel.
- **JOBS:** opens and closes the Job Board.
- **Pause:** use the HUD's **II** button at the top left, next to the wallet. It is shown on PC as well.

The pause menu option **Touch controls** cycles through **Auto** (shown on mobile and touchscreens), **On** (always shown; on a PC the left mouse button acts as a finger, for testing) and **Off**. The choice is saved in `user://settings.cfg`, separately from the game save, so New Game does not reset it.

### Saving, Continue and New Game

The game saves **automatically**:
- after every delivery, failed job and cancelled job;
- every 60 s;
- when you close the window;
- when you press **F5**.

The save file is `user://savegame.json`. It holds your wallet, statistics, driver rating, fuel, city, layout seed and position.

- **Continue:** returns to **the saved city, with the same layout and position**. The menu shows a summary of the save (city, money, deliveries, rating and when it was saved). The button is disabled when there is no save.
- **New Game:** if a save exists, it first asks for confirmation and shows what will be lost. It then deletes the save, resets money, statistics and rating, and starts in a random city. That city is saved right away, so Continue brings you back to it.
- **Not saved:** a job in progress. After Continue, you pick a new job from the board.

### Vehicle shop

Open it with **Vehicle Shop** in the pause menu. The game stays paused while you shop, and **Esc** goes back to the pause menu.

- **Stock:** the shop has **4 vehicles** and restocks at **00:00, 08:00 and 16:00** on your computer's clock. A countdown shows the next restock. The stock is generated from the date and the time slot, so reopening the shop (or restarting the game) in the same slot shows the same vehicles. A vehicle you bought shows **Sold** until the next restock, and this is saved too.
- **Models:** each model has its own base stats and colour.

| Model | Top speed | Fuel | Tank | Base price | Options |
|---|---|---|---|---|---|
| Basic Scooter (starter, not sold) | 67 km/h | 8.0 L/100km | 4 L | $150 | none |
| City Scooter | 72 km/h | 7.0 | 4.5 L | $300 | 0-2 |
| Cargo Moto | 62 km/h | 9.0 | 6 L | $420 | 1-3, often a thermal box |
| Electric Scooter | 65 km/h | 3.5 | 3 L | $600 | 0-2 |
| Sport Bike | 86 km/h | 12.0 | 4 L | $900 | 1-3, often tuned or a crash guard |

- **Options:** each vehicle in the shop is rolled with a random number of options, each with a random strength. Two vehicles of the same model can therefore be very different.

| Option | Effect | Strength |
|---|---|---|
| Tuned engine | Top speed +x (acceleration +x/2) | 5-25% |
| Eco engine | Fuel use -x | 10-40% |
| Thermal box | Timed package jobs (food): late penalty -x **and** fast-delivery tips +x. Not for passengers. A silver box is drawn on the bike | 20-60% |
| Crash guard | Stun after crashing into traffic -x | 20-60% |
| Big tank | Fuel tank +x | 25-75% |

- **Price** = base price + each option's strength × its price per percent, rounded to $10.
- **Buying:** buying **trades in** your current vehicle for 40% of its value (the starter is worth $60). A confirmation shows exactly what you pay. The new vehicle comes with a full tank. The vehicle, its options and the shop's sold list are saved, and New Game gives the starter back.

### Pause menu

Press **Esc** or **P**, or click/tap the **II** button at the top left of the HUD. The whole game freezes: your bike, traffic, traffic lights, weather, job timers, fuel use and play time all stop. The menu shows a short summary (city, weather, money, deliveries, rating, fuel and play time) and these buttons:

| Button | Action |
|---|---|
| Resume (Esc) | Continue playing |
| Save Game | Save now. The menu confirms with "Game saved at 16:23" |
| Save & Main Menu | Save and return to the main menu, where Continue resumes |
| Save & Quit | Save and close the game |

If you have a job in progress, the menu warns that it will be lost when you leave, because jobs are not saved.

## Gameplay loop

0. The game starts on the main menu (`ui/main_menu.tscn`), which lists every city found in `data/cities/`. Press **Continue** to resume your save, or **New Game** to start fresh in a random city. The game ships with **Hanoi**, **Da Nang** and **Ho Chi Minh City**. The HUD shows the current city above the district name.
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
- **Tips:** if you deliver within `TipTimeShare` of the drop-off limit (50% by default), the customer adds a tip of `TipShare` × reward (20% for food, 25% for rides). The job card shows `Tip +$7 if done within 0:07`. The tip is a separate wallet entry, and the board shows the total in `Stats.TotalTips`.

### Traffic and crashes

Bicycles, motorbikes, cars and buses drive on the right-hand lane of every road. At each intersection they go straight or turn. They brake for you and for vehicles ahead going the same way.

Each vehicle type has a **collision score**. If you hit one at a closing speed of about 11 km/h or more, you are **stunned** and cannot drive for `score × 0.6 s`. The bike blinks red, the HUD shows `CRASHED 1.8 s`, and your job timers keep running. After a stun you get 1 s of immunity so crashes cannot chain.

| Vehicle | Collision score | Stun |
|---|---|---|
| Bicycle | 1 | 0.6 s |
| Motorbike | 2 | 1.2 s |
| Car | 4 | 2.4 s |
| Bus | 6 | 3.6 s |

### Traffic lights and fines

Some of the inner intersections have traffic lights: 50% in Hanoi, 35% in Da Nang and 45% in HCMC, set by `traffic.light_chance` in the city JSON. Which intersections get lights depends on the layout seed, so the lights stay in the same places. Each light runs **green 8 s, yellow 2 s, all red 1 s**, then switches to the other direction. Each intersection starts at a random point in its cycle. White stop lines and a signal head on the near-right corner show each approach.

- **AI traffic** stops at the stop line on red. It also stops on yellow if it still can, and otherwise goes through. Vehicles queue behind each other, and a queue at a light is never treated as a jam.
- **You** are fined if you enter a lit intersection while the light for your direction is **red**. Entering on yellow is allowed. The fine is **$20**, and every earlier violation in the last 2 minutes adds another $20, up to ×3 ($60). The money comes out of your wallet, which can go negative. The screen flashes red and a toast shows the fine.
- **Records:** the Job Board shows `Red lights run` and `Fines`, and both are saved (save version 4).

### Fuel and gas stations

The scooter has a **4 L** tank and burns **0.08 L/km**, plus a little while idling (0.02 L/min). That gives about 50 km on a full tank.

- **Gauge:** the HUD shows a fuel bar. Below 30% it also shows the distance to the nearest gas station. You get warnings at 25% and 10%.
- **Empty tank:** you can only **push the bike** at about 8 km/h (`VehicleStats.PushSpeed`). You never get stuck for good.
- **Gas stations:** every city has a few, well spread out and placed from the layout seed, so they stay in the same places. A station is a yellow circle with a red pump and a "GAS" sign. Drive in and press **E** to fill the tank at the city's price per liter (`fuel.price_per_liter` in the city JSON). If you cannot afford a full tank, you buy what your balance covers. With no money you cannot buy fuel.
- **Records:** the Job Board shows the money spent on fuel, and the tank level is saved (save version 5).

### Weather

The weather changes every 1-3 minutes. Each city weights the kinds differently through `weather.weights` in its JSON. The world is tinted to match, rain falls on screen, and storms have lightning flashes.

| Weather | Speed | Grip | Fuel use | Job pay |
|---|---|---|---|---|
| Sunny | 100% | 100% | **×1.4** | 100% |
| Cloudy | 100% | 100% | ×1 | 100% |
| Rain | **75%** | 70% | ×1 | +15% |
| Storm | **60%** | 55% | ×1.05 | +30% |

- **Who is affected:** speed and acceleration apply to you and to AI traffic. Lower grip makes the bike slide more in turns.
- **Pay:** the pay bonus goes through `GameManager.GetRewardMultiplier()`, so jobs on the board pay more while it rains.
- **Display:** the HUD shows the current weather and its effects, and a toast announces each change.

### Time of day (real clock) and fatigue

The game follows your **computer's local clock**. The world's light changes smoothly through the day: dawn, bright midday, sunset, then night. At night your bike and the AI traffic switch on **headlights**, and job markers, gas stations and rest stops glow. Weather tints are mixed on top. The HUD shows the clock and the current part of the day, and a toast announces each new part.

| Part of the day | Hours | Effects |
|---|---|---|
| Morning | 05-10 | **Breakfast rush:** food orders ×3, **heavy traffic** ×1.5 |
| Midday | 10-14 | **Hot:** fuel use ×1.25, fatigue ×1.5 |
| Afternoon | 14-18 | Normal |
| Evening | 18-22 | **Fewer jobs** (3 instead of 5), **pay ×1.3**, fatigue ×1.4, less traffic |
| Night | 22-05 | 2 jobs, pay ×1.5, fatigue ×1.6, quiet streets, dark |

Each part of the day is a `TimePeriodData` .tres in `resources/day_periods/`. AI traffic grows or shrinks towards the new amount a few vehicles at a time, out of your sight.

**Fatigue (0-100%)**, shown as the "Tired" bar on the HUD:
- **Driving** adds about 4% per minute, times the part of the day's multiplier. The longer you drive **without a stop**, the faster it climbs, up to ×2. Every crash adds 2%.
- **Standing still** (after a 2-second pause) recovers about 6% per minute.
- You get warnings at 60% and 70%. Above 60% the HUD also shows the distance to the nearest rest stop.
- At **80%** you are **exhausted**: you can only push the bike (about 8 km/h) and cannot take new jobs until fatigue is back down to **50%**.
- **Rest stops** are street food stalls marked with a teal "REST" sign. Each city has a few, well spread out and away from the gas stations. Drive in and press **E** to eat and rest for the city's price (`rest.price` in the city JSON, $6-8). Fatigue drops to 0 in 4 seconds, and you cannot drive meanwhile. Without enough money, just stop and wait.
- **Records:** fatigue is saved (save version 7), and the Job Board shows the money spent on food and rest.

### Driver rating (1-5 stars)

Every finished or failed job gets a customer rating:

| Outcome | Stars |
|---|---|
| Delivered on time (or an untimed job) | 5 |
| Delivered late | 3 |
| Cancelled by you after accepting | 2 |
| Failed (missed the pickup deadline, or a `LatePenalty = 1` job) | 1 |

- **How the rating is calculated:** it is the average of the last 20 ratings (`Reputation.Window`). A new driver starts with five 5-star ratings, so one bad job drops the rating to about 4.3 rather than to 1.0. Cancelling counts against you, so you cannot dodge a missed deadline by cancelling just before it.
- **Where it shows:** the HUD shows the stars, and the Job Board shows the counters (on time, late, failed, cancelled).
- **A lower rating means fewer high-paying jobs:**
  - **Pay multiplier:** every reward is multiplied by a factor from ×0.80 at 1 star to ×1.10 at 5 stars (`GameManager.GetRewardMultiplier()`).
  - **Job types:** each `JobTemplate` has a `MinRating`. At or below that rating the job type is never offered. Between `MinRating` and 5 stars it appears proportionally less often. The defaults are Fragile Electronics 4.5 and Passenger Ride 3.5, and everything else is open to all drivers. The Job Board lists the locked and rarer types.

## Folder structure

```
autoload/      Singletons: EventBus.cs (signals), GameManager.cs (state), SaveManager.cs (persistence)
scenes/        main, player, city_map, job_marker (.tscn)
data/cities/   City definitions, one JSON file per city (see "Adding a city")
ui/            main_menu (startup scene), pause_menu, shop_panel, hud, job_board, job_entry, delivery_result_popup (.tscn)
scripts/
  Main.cs      Composition root: picks a random city, registers the world, loads the save, opens the board
  player/      Player (motorbike controller), BikeVisual, TargetIndicator, VehicleStats
  map/         CityMap (procedural builder), ServiceStop (base of GasStation / RestStop), CityLoader + CityConfig (JSON -> CityRegionData), Building, CityRegionData, DistrictData, DeliveryLocation
  jobs/        JobManager (state machine), JobGenerator, JobTemplate, JobData, JobMarker, DeliveryResult
  economy/     Wallet (+ WalletEntry), PlayerStats, Reputation (driver rating)
  vehicles/    VehicleCatalog, OwnedVehicle (+ PerkRoll), VehiclePerks (option table), VehicleShop (stock rotation)
  traffic/     TrafficManager (spawner), TrafficVehicle (AI road user), TrafficVehicleData, TrafficLights (signals + fines)
  weather/     WeatherSystem (picks and fades weather), WeatherData, RainOverlay
  daycycle/    DayCycle (real-clock time of day + daylight), TimePeriodData, NightLight (headlights / glows)
  save/        SaveData.cs: JSON save DTOs; GameSettings.cs: preferences (touch controls)
  ui/          MainMenu, PauseMenu, ShopPanel, TouchControls, Hud, JobBoard, JobEntry, DeliveryResultPopup, StarRating (draws 0-5 stars)
resources/
  jobs/        JobTemplate .tres files (one per job type): parcel, documents, fragile_electronics, food_delivery, passenger_ride
  vehicles/    vehicle_catalog.tres + one VehicleStats .tres per model (basic_scooter, city_scooter, cargo_moto, e_scooter, sport_bike)
  traffic/     TrafficVehicleData .tres files: bicycle, motorbike, car, bus
  weather/     WeatherData .tres files: sunny, cloudy, rain, storm
  day_periods/ TimePeriodData .tres files: morning, midday, afternoon, evening, night
  ui/          ui_theme.tres
```

All C# code is in the `ShipperSimulator` namespace. Godot requires each script file to have the same name as its class.

## Architecture

- **EventBus** (`EventBus.Instance`) is the only connection between systems. It declares Godot `[Signal]`s. Listeners subscribe with C# event syntax, for example `EventBus.Instance.JobAccepted += OnJobAccepted;`. Senders call `EmitSignal(EventBus.SignalName.X, ...)`. UI scripts emit requests such as `JobAcceptRequested`, and `JobManager` emits results such as `JobStateChanged`, `JobDelivered`, `JobFailed`, `JobTimerUpdated` (every frame of a timed job), `JobBoardNoticeChanged` and `NavigationTargetChanged`. `GameManager` emits `BalanceChanged`, `StatsChanged` and `ReputationChanged`. Nothing in the UI holds a reference to `JobManager`.
  - Subscribe with **methods, not lambdas**, and **unsubscribe in `_ExitTree()`** with the same method (`-=`). The `EventBus` signals are declared in C#, so their C# events are plain delegates and Godot does **not** disconnect them when a node is freed. A handler you forget to remove keeps running after a scene change and throws `ObjectDisposedException`. Built-in Godot signals such as `Button.Pressed` are disconnected automatically.
- **Scene lifecycle:** `Main._ExitTree()` calls `GameManager.UnregisterWorld()`, so autosave and play-time tracking stop when you leave the gameplay scene.
- **Touch controls** (`TouchControls`, a full-screen Control under `UI`):
  - **Input:** it handles `InputEventScreenTouch` and `InputEventScreenDrag` in `_Input` and tracks each finger by its touch index, so the joystick and BRAKE work at the same time. Mouse events that Godot emulates from touches are ignored.
  - **Output:** it does not move the bike itself. It presses the normal actions: `move_*` with analog strength through `Input.ActionPress`, and taps for `interact` and `toggle_job_board` sent as `InputEventAction` events, so `_UnhandledInput` handlers see them. It only releases actions it pressed itself, so it never cancels keyboard input.
  - **Sharing the screen with the UI:** a touch that starts on a visible control in `BlockingControls` (the Job Board panel, the delivery popup and the active-job panel) is left to the UI, and so is any touch outside its own zones (such as the HUD pause button). JOBS always works.
  - **Pausing:** when the tree pauses or the app loses focus, every touch action is released.
  - **Settings:** they come from `SaveManager.Settings` (`GameSettings`), which `SaveManager.LoadSettings()` / `SaveSettings()` read and write. A save triggers `EventBus.SettingsChanged`.
  - **Mobile export:** `export_presets.cfg` only has a Windows preset. Add an Android or iOS preset in the editor (Project → Export). Godot .NET supports Android, and iOS is experimental. Include `data/cities/*.json` in its `include_filter`, like the Windows preset.
- **Vehicles and the shop:**
  - **Models:** a model is a `VehicleStats` .tres. It holds the handling and fuel values plus the `Shop` group: `ModelId`, `BasePrice`, `ShopWeight` (0 = never sold), `MinPerks`/`MaxPerks`, and `PerkWeights` by option id. `VehicleCatalog` (`resources/vehicles/vehicle_catalog.tres`) lists every model and the starter.
  - **Owned vehicle:** `GameManager.Vehicle` is an `OwnedVehicle` (model id, price, `PerkRoll`s). `BuildStats()` duplicates the model and applies the speed, eco and tank options. `GameManager.ApplyVehicleToPlayer()` hands the result to `Player.SetVehicle()`, together with the colour and the thermal box.
  - **Option effects outside the stats:** Crash guard is read in `Player.Crash()`. The thermal box goes through `GameManager.LatePenaltyFor()` and `GameManager.TipFor()`, which payouts, job cards and the "running late" toast all use.
  - **Shop stock:** `VehicleShop.GenerateOffers(catalog, rotationKey)` is deterministic. It uses `System.Random`, seeded with an FNV-1a hash of the key (`"2026-10-06#1"`), because `string.GetHashCode` differs between runs. Set `ShopPanel.RotationOverride` in the inspector to preview any rotation.
  - **Buying:** `GameManager.BuyVehicle()` charges the price minus the trade-in, marks the offer as sold for that rotation, switches vehicles with a full tank and saves.
  - **Options table:** the options, their ranges and their prices live in `VehiclePerks.All`.
- **Pausing:** the HUD pause button (`focus_mode = None`, so Space and Enter never press it while driving) emits `EventBus.PauseMenuRequested`, which `PauseMenu.Open()` listens to. `PauseMenu` (under the `UI` CanvasLayer) sets `SceneTree.Paused`. It is the only gameplay node with `ProcessMode.Always`, so it keeps handling input while everything else stops. `GameManager` also always processes, so that F5 works while paused, but it skips play time and autosave when the tree is paused. Before changing scene, the pause menu unpauses the tree.
- **GameManager** (`GameManager.Instance`) owns the global state (`Wallet`, `PlayerStats`, `Reputation`, world references) and holds the helpers for payouts and formatting (`FormatMoney`, `FormatDistance`...). It also builds the data that goes into the save file.
- **SaveManager** writes and reads versioned JSON (currently version 3) through `System.Text.Json`, using the DTOs in `scripts/save/SaveData.cs` with snake_case keys. Add migrations in `Migrate()`. The file is `SaveManager.SavePath`, which defaults to `user://savegame.json`. You can change it for save slots or for tests that must not touch the real save.
- **Data resources** (`JobTemplate`, `VehicleStats`) are `[GlobalClass]` resources, so you can create and edit them in the inspector. In `.tres` files their properties use the C# names (PascalCase). `CityRegionData` and `DistrictData` are created at runtime by `CityLoader` from the city JSON files.
- **Classes that never go through Godot** (`Wallet`, `PlayerStats`, `JobGenerator`, `DeliveryLocation`, `CityLoader`, the save and city DTOs) are plain C# classes that use `event Action` instead of Godot signals. `DeliveryResult` is a `RefCounted` because it is passed through a Godot signal.
- **JobManager** runs the job state machine (`Idle → ToPickup → ToDelivery → Idle`), spawns the markers and enforces time limits.
  - Each phase has its own timer (`_phaseTime`, accumulated from `delta`, so it stops if the game is paused).
  - Missing the pickup deadline calls `FailActiveJob()`. Missing the drop-off deadline marks the job late, and `GameManager.CompleteDelivery()` then subtracts `JobData.LatePenalty`.
- **JobTemplate** configures each job type:
  - `Cargo` (`Package` or `Passenger`) changes the texts, the marker icon and the bike visual.
  - `CustomerNames` gives each job a named customer.
  - The **Time limits** group holds `IsTimed`, `PickupBaseTime`, `PickupTimePerKm`, `DeliveryBaseTime`, `DeliveryTimePerKm`, `LatePenalty`, `TipTimeShare` and `TipShare`.
  - The **Reputation** group holds `MinRating`.
- **JobGenerator** turns templates and the map's addresses into `JobData`. The reward is:
  `(BaseReward + km × RewardPerKm) × district multiplier × GameManager.GetRewardMultiplier() ± variance`.
- **CityMap** builds the city from a `CityRegionData` resource. Roads, sidewalks, parks and water are drawn in `_Draw()`. Buildings, trees and water blocks are `StaticBody2D` obstacles on physics layer 2. Every side of every block gets one curbside address, so there are `4 × GridSize.X × GridSize.Y` addresses.
  - **Seed:** each city sets `"seed"` in its JSON file (Hanoi 1010, Da Nang 2020, HCMC 3030). A fixed seed always produces the same layout. `0` produces a new random layout on every load. The seed actually used is `CityMap.ActiveSeed`, and it is saved together with `RegionId`.
  - **Block types:** for each block the generator rolls `DistrictData.WaterChance` first, then `ParkChance`, and otherwise fills the block with building lots. A district with `WaterChance = 1` becomes a river, for example `han_river` and `saigon_river`. You can still drive along the roads that cross it.
- **City selection:** `Main._EnterTree()` loads every city with `CityLoader.LoadAll(Main.CitiesFolder)` before `CityMap._Ready()` builds the map.
  - **Continue** (`GameManager.NextStart == StartMode.Continue`): `SaveManager.ReadSave()` reads the save without applying it. `Main` picks the saved city and sets `CityMap.SeedOverride` to the saved `layout_seed`, so the layout matches the saved position even if the city's seed is 0 (random) or has been changed in its JSON. If the saved city no longer exists, a random city is used.
  - **New Game:** `GameManager.StartNewGame()` deletes the save and resets `Wallet`, `PlayerStats` and `Reputation` in memory. `Main` then picks a random city, saves at once and switches `NextStart` back to `Continue`.
  - **Running `main.tscn` directly** (F6) behaves like Continue.

Physics layers: 1 = player, 2 = world, 3 = interactables (markers), 4 = traffic. The player collides with layers 2 and 4 (`collision_mask = 10`).

- **Traffic:**
  - **Spawning:** `TrafficManager` (the `Traffic` node in `main.tscn`) spawns `TrafficVehicle`s after the map is built. It never spawns them near the player's spawn point.
  - **Driving:** each vehicle is an `AnimatableBody2D` on layer 4 that is moved by setting `Position`, with `SyncToPhysics` off. It follows waypoints from the entry point of one intersection to the exit point of the next.
  - **Braking:** an `Area2D` sensor in front of each vehicle brakes for the player and for traffic heading the same way. Crossing traffic is ignored, so vehicles can briefly overlap in the middle of an intersection. If a vehicle waits behind other traffic for more than 3 s, it ignores that traffic for a moment, which clears jams.
  - **Crashes:** `Player.CheckTrafficCrash()` compares the closing speed along the contact normal with `MinCrashSpeed` and then calls `Crash()`. That sets `StunTimeLeft`, pushes the bike back, makes the vehicle stop for 1.5 s and emits `EventBus.PlayerCrashed`.
  - **Tuning:** vehicle sizes, colors, speeds, `CollisionScore` and `SpawnWeight` are in `resources/traffic/*.tres`. The seconds per collision point are set by `TrafficVehicleData.StunSecondsPerPoint`.
- **Fuel:**
  - **Player:** `Player` holds `Fuel`. `ConsumeFuel()` charges for the distance moved each physics frame (jumps over 200 px, such as teleports, are ignored) plus idle use, multiplied by the weather's `FuelMultiplier`. With an empty tank, `MaxForwardSpeed` drops to `PushSpeed`.
  - **Stations:** `CityMap.BuildGasStations()` turns well-spread curbside addresses into `GasStation`s using farthest-point sampling and its own seeded RNG. Those addresses are removed from the job locations.
  - **Buying:** `GasStation` handles **E** and calls `GameManager.BuyFuel()`.
  - **Settings:** tank size and consumption are on `VehicleStats` (`FuelCapacity`, `FuelPerKm`, `IdleFuelPerMinute`, `PushSpeed`).
- **DayCycle** (the `DayCycle` node in `main.tscn`, placed before `Weather` so it is ready first):
  - **Clock:** twice a second it reads `DateTime.Now`, or `ClockOverrideHour` (inspector, for testing any hour). It sets `Hour`, the daylight `LightColor` (interpolated from hourly keyframes), `Darkness` (0-1) and the active `TimePeriodData` (`GameManager.Period`). When the period changes, it emits `EventBus.PeriodChanged`.
  - **Period effects:**
    - `JobManager` multiplies the number of jobs per board by `JobCountMultiplier`.
    - `JobGenerator` multiplies template weights by `TemplateWeights` (for example `"food": 3` in the morning).
    - `GetRewardMultiplier()` includes `RewardMultiplier`, and `Player.ConsumeFuel()` includes `FuelMultiplier`.
    - Fatigue uses `FatigueMultiplier`, and `TrafficManager` aims for its normal count × `TrafficMultiplier`.
  - **Lighting:** `WeatherSystem` sets the `CanvasModulate` to its weather tint × `DayCycle.LightColor`. `NightLight` is a `PointLight2D` whose energy follows `Darkness`. It provides the headlights (`NightLight.Headlight`, on `Player` and `TrafficVehicle`) and the glows (`NightLight.Glow`, on `JobMarker` and `ServiceStop`). Its gradient texture is created on first use, never in a field initializer.
- **Fatigue and rest stops:**
  - **Player:** fatigue lives on `Player`, and its rates and thresholds are exported in the "Fatigue" group. `UpdateFatigue()` runs every physics frame. While `IsExhausted`, `MaxForwardSpeed` drops to `PushSpeed` and `JobManager.AcceptJob()` refuses new jobs.
  - **Rest stops:** `RestStop` and `GasStation` share `ServiceStop` (trigger zone, prompt, E, night glow). `CityMap.TakeSpreadLocations()` places both with farthest-point sampling. `GameManager.Rest()` charges the price and calls `Player.StartRest()`.
- **WeatherSystem** (the `Weather` node in `main.tscn`):
  - **Registration:** it registers itself as `GameManager.Weather`.
  - **Picking:** `WeatherData` is chosen by the city weights, avoiding a repeat of the current kind. Its duration is random between `MinDuration` and `MaxDuration`.
  - **Visuals:** a `CanvasModulate` tints the world, and a `RainOverlay` on CanvasLayer 5 draws the rain. The UI is on layer 10, above both. Changing weather fades both over a few seconds.
  - **Effects:** `Player` reads the speed, acceleration, grip and fuel multipliers, `TrafficVehicle` reads the speed multiplier, and `GetRewardMultiplier()` reads the pay multiplier.
  - **Testing:** `ForceWeather(id)` switches weather on demand.
- **TrafficLights** (the `TrafficLights` node in `main.tscn`):
  - **Signals:** `GetSignal(node, dir)` and `GetSignalForAxis(node, horizontal)` return `None`, `Green`, `Yellow` or `Red`. A vehicle asks for the signal of the intersection it is driving towards, via `TrafficVehicle.DistanceToStopLine()`.
  - **Enforcement:** each physics frame, `CheckPlayer()` finds the intersection the player is in. On entering, it uses the side the player came from to pick the east-west or north-south light. It ignores entries when the player is stunned or slower than `MinViolationSpeed`.
  - **Fines:** `GameManager.ApplyTrafficFine()` takes the money and records the violation, then `EventBus.TrafficFined` updates the HUD.
  - **Settings:** timings (`GreenTime`, `YellowTime`, `AllRedTime`) and fines (`BaseFine`, `MaxFineMultiplier`, `RepeatWindow`) are exported on the node.

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
  },
  "traffic": {
    "count": -1,                    // number of AI vehicles; -1 = about 0.6 per block
    "weights": { "motorbike": 8, "car": 2, "bicycle": 3, "bus": 0.5 },  // ids from resources/traffic
    "light_chance": 0.45            // share of inner intersections with traffic lights (0-1)
  },
  "fuel": {
    "price_per_liter": 2.5,         // money per liter at the gas stations
    "stations": -1                  // number of gas stations; -1 = about one per 12 blocks (min 3)
  },
  "weather": {
    "weights": { "sunny": 3, "cloudy": 1.5, "rain": 2.5, "storm": 0.5 }  // ids from resources/weather
  },
  "rest": {
    "price": 8,                     // meal + rest at a rest stop
    "stops": -1                     // number of rest stops; -1 = about one per 16 blocks (min 2)
  }
}
```

`CityLoader` validates every file. A file with broken JSON, a missing `code`/`name`, a district outside the grid or a duplicate `code` is **skipped**, and the reason appears in Godot's Output/Debugger panel. Mistakes that still produce a playable city, such as blocks not covered by any district or the wrong number of street names, only cause a warning. The Windows export preset includes `data/cities/*.json` through `include_filter`. Add the same filter to any new export preset, otherwise the exported game will have no cities.

## Where future features plug in

| Feature | Extension point |
|---|---|
| More parts of the day | Add a `TimePeriodData` .tres (with its `StartHour`) to `Periods` on the `DayCycle` node. |
| Fatigue tuning | Change the exported "Fatigue" values on the Player in `scenes/player.tscn`, or the `FatigueMultiplier` of each period. |
| More weather | Add a `WeatherData` .tres (for example fog: `SpeedMultiplier` 0.85 and a gray `Tint`) to `WeatherTypes` on the `Weather` node, then weight it in the city JSON. |
| New vehicle model | Add a `VehicleStats` .tres (with a unique `ModelId` and shop data) to `vehicle_catalog.tres`. |
| New vehicle option | Add a `VehiclePerkType` and a row in `VehiclePerks.All`, then apply it in `OwnedVehicle.BuildStats()` or wherever it acts. Saves store options by id. |
| More traffic types | Add a `TrafficVehicleData` .tres (for example a truck: `Shape = Bus`, a higher `CollisionScore`) and add it to `VehicleTypes` on the `Traffic` node. Cities can weight it by `VehicleId`. |
| More traffic laws | Follow the `TrafficLights.CheckPlayer()` pattern (for example speeding in a district, or driving against traffic) and charge with `GameManager.ApplyTrafficFine()`. |
| Crash consequences | Listen to `EventBus.PlayerCrashed`, for example to damage fragile cargo, upset passengers (`Reputation`) or count crashes in `PlayerStats`. |
| Reputation | Add a stat to `PlayerStats` and `StatsSaveData`, and adjust it in `GameManager.CompleteDelivery()`. Expose it through `GetRewardMultiplier()` and add fields to `DeliveryResult`. |
| More cities | Add a JSON file to `data/cities/` (see "Adding a city"). A city picker on the menu could pass the chosen `code` to `Main` in place of the random pick in `Main._EnterTree()`. |
| Modded cities | Call `CityLoader.LoadAll("user://cities")` as well, so players can add JSON files without rebuilding the game. |
| More timed jobs | Set `IsTimed = true` on any `JobTemplate` .tres. Time limits and penalties need no code. |
| Rating rules | Star values, the window size and the pay range are constants in `Reputation`. The per-job gates are `JobTemplate.MinRating`. |
| Rating perks | Read `GameManager.Instance.Reputation.Rating`, for example to unlock vehicles or bonus jobs, and listen to `EventBus.ReputationChanged`. |
| Saving the active job | `JobData` only holds plain data. Add a DTO for it to `GameSaveData`. |
