# Smart-X IoT Mesh Gateway (PROG7312 POE Part 1, Part 2 and Part 3)

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

### Part 3 additions

- Historical telemetry range queries: `SmartX.Api/Data/SensorHistoryAvlTree.cs`, a real self balancing avl tree, one per sensor in `SensorHistoryTreeStore.cs`, so pulling a time range for one sensor never touches another sensor's history
- Mesh network graph: `SmartX.Api/Data/MeshNetworkGraph.cs`, vertices are sensor mac addresses plus a virtual gateway, edges are simulated wireless links weighted by a deterministic power cost
- Graph traversal: same file, `Bfs()` and `Dfs()`, run from the gateway to work out which sensors are actually reachable right now
- Minimum spanning tree: same file, `ComputeMst()`, prim's algorithm from the gateway outward, this is the most power efficient backbone for telemetry to hop through the mesh
- Self healing: `MeshTopologyStore.cs` builds the graph fresh from whichever sensors are currently online (the same 30 second disconnect check the dashboard already uses), an offline sensor gets no edges at all, so bfs/dfs and the mst both stop reaching it automatically and pick it back up the moment it starts reporting again
- Network Topology and Mesh Routing page: `SmartX.Client/NetworkTopologyForm.cs`, draws the live mesh as a node link diagram, tracks a sensor's routing path to the gateway along the mst, and queries the avl tree for a sensor's history in a chosen time window
- Main menu: all three pillars are enabled now and can be open at the same time, same non modal pattern as the other two

### Part 3 code examples

**AVL tree rebalancing** (`SmartX.Api/Data/SensorHistoryAvlTree.cs`), this is what keeps a range query logarithmic instead of linear, every insert rebalances immediately so the tree never degrades into a straight line no matter what order readings arrive in:

```csharp
private static Node RotateRight(Node node)
{
    var newRoot = node.Left!;
    node.Left = newRoot.Right;
    newRoot.Right = node;
    node.Height = 1 + Math.Max(Height(node.Left), Height(node.Right));
    newRoot.Height = 1 + Math.Max(Height(newRoot.Left), Height(newRoot.Right));
    return newRoot;
}
```

`RotateLeft` is the mirror image of this. `Rebalance()` compares left and right subtree heights after every insert and calls one or both rotations when the tree has tipped more than one level out of balance, this is what guarantees `O(log n)` lookups even under a worst case strictly increasing or decreasing stream of timestamps, which is exactly how real telemetry arrives.

**Prim's minimum spanning tree** (`SmartX.Api/Data/MeshNetworkGraph.cs`), starts from the gateway vertex and always grows by the cheapest edge reachable from whatever is already in the tree:

```csharp
while (inTree.Count < Vertices.Count)
{
    string? bestFrom = null;
    string? bestTo = null;
    int bestWeight = int.MaxValue;

    foreach (var vertex in inTree)
    {
        foreach (var (neighbor, weight) in GetNeighbors(vertex))
        {
            if (!inTree.Contains(neighbor) && weight < bestWeight)
            {
                bestWeight = weight;
                bestFrom = vertex;
                bestTo = neighbor;
            }
        }
    }

    if (bestTo == null)
    {
        break;
    }

    edges.Add((bestFrom!, bestTo, bestWeight));
    inTree.Add(bestTo);
    totalWeight += bestWeight;
}
```

The `if (bestTo == null) break;` is what makes this safe to run against a partially offline mesh, if the vertices already in the tree have no cheaper edge left to anything outside it, the loop stops instead of looping forever, which is exactly what happens when some sensors are disconnected and have no edges in the graph at all.

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
6. Send three rising (or three falling) readings in a row for the same sensor, each one closer to its category's upper or lower limit but not past it yet, e.g. for an Environmental sensor (range 0-100) send 70, then 82, then 92. Before the sensor actually goes anomalous, Automated Insights will already flag it as "trending toward its limit", this is a proactive warning built from the sorted timeline history, not a reaction to something that already broke

**To test the SortedDictionary timeline view:** select any row in the Command Stream grid and click View Timeline. Every reading the background service processes is recorded in `TimestampedLogStore` in timestamp order automatically, and this draws it as a real line graph, oldest reading on the left. Click Refresh on the timeline window while telemetry keeps coming in to watch it grow live.

## Testing the Part 3 data structures

Open **Network Topology and Mesh Routing** from the main menu, it can be open alongside both other pillars at once, all three update live and independently.

**To test the avl tree range queries (Trees):**
1. Register a sensor and send it a few readings a few seconds apart, either let the dashboard simulate it or hit the api directly
2. On the Network Topology page, pick that sensor under Historical Log, set a From and To time that only covers some of the readings you just sent
3. Click Load History, only the readings inside that window come back, not the sensor's entire history, this is the avl tree's range query doing the filtering, not a full scan of everything the sensor has ever reported. `GET /api/topology/{sensorId}/history?from=<ticks>&to=<ticks>` is the endpoint behind it if you want to call it directly

**To test the mesh graph, bfs/dfs reachability and the mst (Graphs):**
1. Register two or three sensors and let each report at least once so they count as online
2. The diagram draws every sensor around a central Gateway node, thin grey lines are the full mesh, thick green lines are the current minimum spanning tree
3. Pick a sensor under Track Routing Path and click Track, its path to the gateway is listed and highlighted in orange on the diagram, sometimes this hops through another sensor instead of going straight to the gateway if that works out cheaper
4. Stop sending that sensor readings and wait 30+ seconds, the sensor turns red on the diagram and drops out of both the mesh and the mst, tracking it again correctly reports it has no route
5. Send it a reading again, within a couple of refreshes it reappears on the diagram and gets a route back

`GET /api/topology/graph`, `GET /api/topology/reachable` (add `?useDfs=true` for depth first instead of breadth first) and `GET /api/topology/mst` are the three endpoints behind all of this, each one rebuilds the graph fresh from whichever sensors are online at that moment.
