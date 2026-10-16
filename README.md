# Smart-X IoT Mesh Gateway (PROG7312 POE Part 1 and Part 2)

Data ingestion, validation and real-time processing gateway for the Smart-X IoT scenario. Built with .NET 10.

## Project layout

- `SmartX.Shared` - class library with the models and data structures shared by both apps
- `SmartX.Api` - ASP.NET Core Web API, receives and stores telemetry
- `SmartX.Client` - Windows Forms app, the dashboard an engineer uses to register sensors and watch telemetry come in

## What each part of the brief maps to

- Generics: `SmartX.Shared/TelemetryPacket.cs`
- Operator overloading: `SmartX.Shared/SensorMeter.cs`, used in the dashboard's "Aggregate Power Load" button
- Jagged arrays into `List<T>`: `SmartX.Shared/TelemetryHistoryStore.cs`, used both server side (telemetry history) and client side (local simulation history)
- Recursion: `SmartX.Shared/DeploymentNode.cs`, used when registering a sensor to check the location path (e.g. `Facility A > Zone 1 > Sub-Zone B`) is a valid nested config
- File upload: `AttachmentsController` on the API, `OpenFileDialog` on the client
- Dynamic engagement feature: anomaly highlighting, sensor rows in the dashboard change colour when a reading is out of range or a sensor stops reporting (matches the Task 1 research report)
- Server side validation: `SensorsController` rejects a bad mac address format, an unknown category, and duplicate mac addresses, and returns the real reason so the client can show it

### Part 2 additions

- Main menu navigation: `SmartX.Client/MainForm.cs`, both pillars are non-modal and single-instance tracked, so they can be open and updating at the same time instead of blocking each other, clicking a button again brings the existing window forward rather than opening a duplicate
- `Queue<T>`: `SmartX.Api/Data/TelemetryIngestionQueue.cs`, standard FIFO path for incoming telemetry
- `PriorityQueue`: same file, critical alerts (severe spikes/drops) bypass the standard queue entirely
- Background processing engine: `SmartX.Api/Data/TelemetryProcessingService.cs`, a `BackgroundService` that drains the queues continuously, critical first, this is what actually applies readings now instead of the controller doing it inline
- `Stack<T>`: `SmartX.Api/Data/CommandHistoryStack.cs`, manual override command history with undo
- `Dictionary<TKey,TValue>`: `SmartX.Api/Data/DeviceRegistry.cs`, devices keyed by MAC address for O(1) lookup, used when a command targets a device by MAC
- `SortedDictionary`: `SmartX.Api/Data/TimestampedLogStore.cs`, every reading kept in timestamp order automatically, drawn as a real line graph in `SmartX.Client/TimelineForm.cs`
- `HashSet<T>`: `SmartX.Api/Data/DeviceAlertTracker.cs`, dedupes repeated disconnect/anomaly alerts for the same device
- Predictive Action Engine: `SmartX.Api/Data/ActionPatternEngine.cs`, tracks which actions (searches or commands) tend to follow which, plus `ActionsController` blends that with live anomaly flags into "Suggested Actions / Automated Insights"
- Real-Time Command Stream page: `SmartX.Client/CommandStreamForm.cs`, live sensor grid, search/filter, manual override commands (trigger pump / toggle relay / shut down all valves), undo, suggestions panel, command history, per sensor timeline view

## Prerequisites

- .NET 10 SDK
- Windows, to actually run the client (the API can run anywhere, the client needs Windows Forms)

## If Visual Studio refuses to build this (NETSDK1100 / "does not support targeting .NET 10.0")

This is a known issue and does not mean the project is broken. Visual Studio's own IDE build tooling has a separate, older version check for which target frameworks it recognises, independent of which .NET SDKs are actually installed on the machine. Even with the .NET 10 SDK installed, pressing F5 in an older Visual Studio release can still fail with this exact error.

**Fix: build and run from a terminal instead of using F5.** This uses the installed SDK directly and is not affected by the IDE-level check:

```
cd SmartX.Api
dotnet run
```

and in a second terminal:

```
cd SmartX.Client
dotnet run
```

Confirm the .NET 10 SDK is actually installed first with `dotnet --list-sdks` (should list a `10.x` entry). If it is not, install it from https://dotnet.microsoft.com/download/dotnet/10.0 first.

## Restoring and building

From the solution root:

```
dotnet restore
dotnet build
```

## Running the API

```
cd SmartX.Api
dotnet run
```

The API listens on `http://localhost:5154` by default (see `Properties/launchSettings.json`). Leave this running, the client needs it.

## Running the client

Open `Prog7312Initial.sln` in Visual Studio and run `SmartX.Client`, or from a terminal:

```
cd SmartX.Client
dotnet run
```

Make sure the API is already running first. The client reads the API address from `SmartX.Client/appsettings.json` (`ApiBaseUrl`), which defaults to `http://localhost:5154/`. Change that file if you run the API on a different port.

## Using the app

1. Start the API, then start the client
2. Click "Sensor Data Ingestion and Telemetry" on the startup menu
3. The dashboard seeds itself with 5 mock sensors the first time it opens, and starts simulating telemetry every 2 seconds
4. Rows turn pink when a reading is out of range, grey when a sensor stops reporting for 30+ seconds
5. Click "Register New Sensor" to add your own, with MAC address, a location path (e.g. `Facility A > Zone 1 > Sub-Zone B`) and a category, optionally attaching a file
6. Click "Aggregate Power Load" to sum up every power consumption sensor's current reading

The "Network Topology and Mesh Routing" button is still disabled on purpose, that's the final PoE.

## Testing the Part 2 data structures

Open **Real-Time Command Stream and History** from the main menu alongside the ingestion dashboard, both windows are non-modal now, so you can have both open side by side, each updating live every 2 seconds independently. Clicking a main menu button again just brings the existing window to the front instead of opening a duplicate.

**To simulate a high-priority alert (Queue vs PriorityQueue):**
1. On the Sensor Data Ingestion dashboard, register or use an existing Environmental or PowerConsumption sensor
2. Normal simulated readings go through the standard queue. To force a critical one, either wait for the dashboard's own random 15% spike chance, or hit the API directly:
   ```
   curl -X POST http://localhost:5154/api/telemetry -H "Content-Type: application/json" -d "{\"sensorId\":\"<id>\",\"value\":4500}"
   ```
   A PowerConsumption reading over 4000, or an Environmental reading under 5 or over 150, counts as critical and jumps the priority queue instead of the standard one
3. Check `GET /api/telemetry/queue-status` immediately after, you can see the critical count tick up before the background service drains it a moment later

**To test manual override commands and undo (Stack):**
1. On the Command Stream page, select a command type (Trigger Pump, Toggle Relay, or Shut Down All Valves)
2. For Trigger Pump or Toggle Relay, type the target device's MAC address (this is the Dictionary-based lookup, `DeviceRegistry`, resolving by MAC)
3. Click Issue Command, the grid's Override column updates, and the Sensor Data Ingestion dashboard will show the same actuator's simulated reading reflecting it (1 for on, 0 for off) within a couple of seconds
4. Click Undo Last, the most recently issued command is reverted, including a bulk Shut Down All Valves command, every device it touched gets its individual previous state back

**To test search/filter and the Predictive Action Engine:**
1. Type a device MAC or location into Search and click Search, the grid filters to matching rows, and the search term is recorded as an action
2. Use the Category dropdown or the Active Alerts Only checkbox to narrow the grid further
3. Issue a command shortly after searching for a related device a few times, the Suggested Actions panel will start recommending that command the next time you search for the same term, this is the pattern engine picking up on repeated search-then-command behaviour
4. Any sensor currently out of range or disconnected shows up under Automated Insights automatically, without needing to search for it first
5. Search for a problem device's MAC a few times, then fix it with a command each time. Once the pattern engine has seen that happen 2-3 times, the next time that exact device goes into an alert state, its Automated Insight line will include a learned fix, e.g. "...is out of range, last reading 9.0 -> try TriggerPump (followed this device 3 time(s) before)", not just the bare flag

**To test the SortedDictionary timeline view:** select any row in the Command Stream grid and click View Timeline. Every reading the background service processes is recorded in `TimestampedLogStore` in timestamp order automatically, and this draws it as a real line graph, oldest reading on the left. Click Refresh on the timeline window while telemetry keeps coming in to watch it grow live.
