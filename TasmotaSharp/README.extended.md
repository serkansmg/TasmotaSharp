# TasmotaSharp – README

A lightweight **HTTP-based Tasmota client** for .NET. Wraps Tasmota’s `/cm?cmnd=` API with pleasant, typed async methods for **relays**, **timers**, **rules**, **time/zone/DST**, **Wi-Fi**, **MQTT**, **sensor reads**, **backup/restore**, and more.

---

## Installation

Just drop the single file into your project:

* `TasmotaSharp/TasmotaClient.cs`

> Namespace is `TasmotaSharp`. Adjust if you prefer.

**Requirements:** .NET 6+ (recommended), `System.Text.Json`.

---

## Quick start

```csharp
using TasmotaSharp;

var client = new TasmotaClient("10.0.4.41");

// Relay
await client.SetRelayAsync(1, true);      // ON
bool? isOn = await client.GetRelayStateAsync(1);

// Time / timezone / DST
await client.SetTimezoneAsync(3);         // UTC+3
await client.SetDstAsync(false);          // DST off
await client.SetTimeAsync(DateTime.Now);  // set device time

// Sensors (Status 10)
var sns = await client.GetSensorStatusAsync();

// Weekly schedule (Timers)
await client.SetTimerAsync(1, new TimeSpan(18,30,0),
    new[]{ DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Saturday }, 1, 1); // 18:30 ON
await client.SetTimerAsync(2, new TimeSpan(23,0,0),
    new[]{ DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Saturday }, 1, 0); // 23:00 OFF

// Multi-relay schedule (same days/times for many outputs)
await client.SetTimerMultiAsync(
    days: new[]{ DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday },
    onTimeHHmm: "09:00",
    offTimeHHmm:"22:00",
    outputs: new[]{1,2,3},
    strategy: MultiScheduleStrategy.Timers,
    startTimerIndex: 1
);

// One-shot at absolute date/time (turn ON then revert after 30s)
await client.SetOneShotDateRuleAsync(
    ruleIndex: 1,
    whenLocal: new DateTime(2025,9,5,18,30,0),
    output: 1,
    onWhenTrue: true,
    pulse: TimeSpan.FromSeconds(30)
);

// “Now + 10s” relative pulse
await client.ScheduleOneShotInAsync(TimeSpan.FromSeconds(10), 1, true, TimeSpan.FromSeconds(10));

// Rule read/delete
var r1 = await client.GetRuleInfoAsync(1);
await client.DeleteRuleAsync(1);

// Wi-Fi scan
var nets = await client.ScanWifiAsync();

// Config backup/restore
var bytes = await client.BackupConfigAsync();
await client.RestoreConfigAsync(bytes);
```

---

## Feature highlights

* **Relays:** `SetRelayAsync`, `ToggleRelayAsync`, `GetRelayStateAsync`
* **Time/Timezone/DST:** `GetTimeAsync`, `SetTimeAsync`, `SetTimezoneAsync`, `SetDstAsync`
* **Status/Sensors:** `GetStatusAsync` (Status 0), `GetSensorStatusAsync` (Status 10)
* **Wi-Fi:** `SetWifiCredentialsAsync`, `GetWifiInfoAsync`, `ScanWifiAsync`
* **MQTT:** `SetMqttAsync`, `GetMqttStatusAsync`
* **Scheduling**

    * **Timers (weekly):** `SetTimerAsync`, `SetTimerByMaskAsync`, `EnableAllTimersAsync`, `DisableTimerAsync`, `ClearTimerAsync`
    * **Rules (one-shot/relative/sunrise-sunset):** `SetOneShotDateRuleAsync`, `ScheduleOneShotInAsync`, `PulseAfterAsync`, `SetRuleScriptAsync`, `EnableRuleAsync`, `GetRuleInfoAsync`, `GetAllRulesAsync`, `ClearRuleAsync`
    * **Multi-relay orchestration:** `SetTimerMultiAsync`
* **Geo:** `SetGeoAsync` (for Sunrise/Sunset rules)
* **mDNS:** `EnableMdnsAsync`, `GetMdnsStateAsync`
* **System:** `RestartAsync`, `FactoryResetAsync`
* **Backup/Restore:** `BackupConfigAsync` (`/dl`), `RestoreConfigAsync` (`/u`)

---

## Real-world use cases

### 1) Weekdays 08:30–20:15 ON/OFF for relays 1–4

```csharp
await client.SetTimerMultiAsync(
    days: new[]{ DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
    onTimeHHmm: "08:30",
    offTimeHHmm:"20:15",
    outputs: new[]{1,2,3,4},
    strategy: MultiScheduleStrategy.Timers,
    startTimerIndex: 1
);
```

### 2) Turn ON 15 minutes after sunset for 5 minutes

```csharp
await client.SetGeoAsync(41.0082, 28.9784); // Istanbul
var rule = new SimpleRule {
    Type = SimpleRuleType.SunriseSunset,
    UseSunset = true,
    Output = 1,
    OnWhenTrue = true,
    OffsetMinutes = 15,
    Pulse = TimeSpan.FromMinutes(5),
    AutoDisable = false
};
await client.ApplySimpleRuleAsync(ruleIndex: 1, rule, timerIndexForRelative: 1);
```

### 3) Fire a short pulse after 30 seconds (5s ON)

```csharp
await client.PulseAfterAsync(
    startDelaySeconds: 30,
    output: 1,
    pulseSeconds: 5,
    ruleIndex: 1,
    timerIndex: 1
);
```

### 4) MQTT setup

```csharp
await client.SetMqttAsync(
    host: "10.0.4.10",
    port: 1883,
    user: "smg",
    password: "secret",
    clientId: "shelf-speaker-01",
    topic: "tasmota/shelf01",
    fullTopic: "%prefix%/%topic%/"
);
var mqtt = await client.GetMqttStatusAsync();
```

### 5) Wi-Fi credentials + restart

```csharp
await client.SetWifiCredentialsAsync("OfficeWiFi", "StrongPass!", restartAfter: true);
```

---

## API cheat-sheet

### Relay

* `Task<bool?> SetRelayAsync(int relay, bool state)`
* `Task<bool?> ToggleRelayAsync(int relay)`
* `Task<bool?> GetRelayStateAsync(int relay)`

### Time / Timezone / DST

* `Task<DateTime?> GetTimeAsync()`
* `Task<bool> SetTimeAsync(DateTime dt)`
* `Task<bool> SetTimezoneAsync(int offset)` → TR: `3`
* `Task<bool> SetDstAsync(bool enabled)` → `SetOption36`

### Status & Sensors

* `Task<TasmotaStatus?> GetStatusAsync()` → maps `Status 0`
* `Task<TasmotaSensorStatus?> GetSensorStatusAsync()` → maps `Status 10`

### Wi-Fi

* `Task<bool> SetWifiCredentialsAsync(string ssid, string password, bool restartAfter=true)`
* `Task<(StatusNET? Net, Wifi? Wifi)?> GetWifiInfoAsync()`
* `Task<List<WifiScanResult>?> ScanWifiAsync(int timeoutMs=5000, int pollIntervalMs=400)`

### MQTT

* `Task<bool> SetMqttAsync(string host, int? port=null, string? user=null, string? password=null, string? clientId=null, string? topic=null, string? fullTopic=null, bool reconnect=true)`
* `Task<StatusMQT?> GetMqttStatusAsync()`

### Scheduling – Timers

* `Task<bool> SetTimerAsync(int index, TimeSpan time, IEnumerable<DayOfWeek> days, int output, int action, bool repeat=true, int mode=0, int window=0)`
* `Task<bool> SetTimerByMaskAsync(int index, string daysMask, string timeHHmm, int output, int action, bool repeat=true, int mode=0, int window=0)`
* `Task<bool> EnableAllTimersAsync(bool enable)`
* `Task<bool> DisableTimerAsync(int index)`
* `Task<bool> ClearTimerAsync(int index)`

> **Days mask:** 7 chars for `Sun..Sat`. `'-'` = off, any char = on. Example: `-1-1-1-` (Mon, Wed, Fri).

### Scheduling – Rules

* `Task<bool> SetOneShotDateRuleAsync(int ruleIndex, DateTime whenLocal, int output, bool onWhenTrue, TimeSpan? pulse=null)`
* `Task<bool> ScheduleOneShotInAsync(TimeSpan delay, int output, bool onWhenTrue, TimeSpan? pulse=null, int ruleIndex=1)`
* `Task<bool> PulseAfterAsync(int startDelaySeconds, int output, int pulseSeconds, int ruleIndex=1, int timerIndex=1)`
* `Task<bool> EnableRuleAsync(int ruleIndex, bool enable)`
* `Task<bool> SetRuleScriptAsync(int ruleIndex, string script)`
* `Task<bool> ClearRuleAsync(int ruleIndex)` / `DeleteRuleAsync(int ruleIndex)`
* `Task<RuleInfo?> GetRuleInfoAsync(int ruleIndex)`
* `Task<List<RuleInfo>> GetAllRulesAsync()`

### Simple rule model (high-level)

* `Task<bool> ApplySimpleRuleAsync(int ruleIndex, SimpleRule rule, int timerIndexForRelative=1)`
* `Task<SimpleRule?> GetSimpleRuleAsync(int ruleIndex)`

### Geo / mDNS / System / Backup

* `Task<bool> SetGeoAsync(double latitude, double longitude)`
* `Task<bool> EnableMdnsAsync(bool enable)` / `Task<bool?> GetMdnsStateAsync()`
* `Task<bool> RestartAsync()` / `Task<bool> FactoryResetAsync(int mode)` (`1`, `2`, or `5`)
* `Task<byte[]?> BackupConfigAsync()` (`/dl`) / `Task<bool> RestoreConfigAsync(byte[] dmpData)` (`/u`)

---

## Tips & caveats

* All methods are **async**; don’t block UI threads.
* **Rules `Delay` unit is deciseconds** (1/10 s). For 5 s use `Delay 50`.
* **Timers:** typical max **16** entries. `SetTimerMultiAsync` uses **2** per output (ON/OFF).
* **Rules:** only `Rule1..Rule3`. One-shot helpers may use an auxiliary rule (for `Status 7` trigger).
* **Day restrictions:** prefer **Timers** for day-specific schedules. `RuleBacklog` day masking is possible but more complex.
* API is tolerant to Tasmota field variations (`SSID/SSId`, `RSSI/Signal` etc.).
* The class owns its **HttpClient**. For heavy multi-device scenarios you can refactor to inject a shared instance.
* Plain HTTP; keep devices on a trusted network and use Tasmota auth if available.

---

## Troubleshooting

**Rule didn’t fire at the expected minute**

* Ensure device time is correct (`GetTimeAsync` / `SetTimeAsync`).
* One-shot uses pattern `-MM-dd'T'HH:mm` against `StatusTIM#Local`.
* Did you enable the rule? (`EnableRuleAsync(ruleIndex, true)`).

**Wi-Fi scan returns nothing**

* The device reports `WifiScan: "Scanning"` while in progress. Increase `timeoutMs`/`pollIntervalMs`.

**Not enough Timer slots**

* You have 16 total. Use `RuleBacklog` or split the plan across devices or different times.

---

## Example: Minimal **Console App** (`Program.cs`)

```csharp
// <Project Sdk="Microsoft.NET.Sdk">
//   <PropertyGroup>
//     <OutputType>Exe</OutputType>
//     <TargetFramework>net8.0</TargetFramework>
//     <Nullable>enable</Nullable>
//   </PropertyGroup>
// </Project>

using TasmotaSharp;

static async Task<int> Main(string[] args)
{
    var host = args.Length > 0 ? args[0] : "10.0.4.41";
    var client = new TasmotaClient(host);

    Console.WriteLine($"Connected to {host}");

    // Toggle relay 1
    var state = await client.ToggleRelayAsync(1);
    Console.WriteLine($"Relay1 is now: {(state == true ? "ON" : "OFF")}");

    // Schedule Mon/Wed/Sat 18:30 ON, 23:00 OFF
    await client.SetTimerAsync(1, new TimeSpan(18,30,0), new[]{ DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Saturday }, 1, 1);
    await client.SetTimerAsync(2, new TimeSpan(23, 0,0), new[]{ DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Saturday }, 1, 0);

    // One-shot in 10s, pulse 10s
    await client.ScheduleOneShotInAsync(TimeSpan.FromSeconds(10), 1, true, TimeSpan.FromSeconds(10));

    // Print Wi-Fi networks
    var nets = await client.ScanWifiAsync();
    if (nets != null)
    {
        Console.WriteLine("Nearby APs:");
        foreach (var ap in nets)
            Console.WriteLine($"  {ap.SSID} ch{ap.Channel} {ap.SignalDbm} dBm ({ap.Encryption})");
    }

    return 0;
}
```

---

## Example: **Worker Service** (Background service with DI)

`TasmotaWorker.cs`:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TasmotaSharp;

public sealed class TasmotaWorker(ILogger<TasmotaWorker> logger) : BackgroundService
{
    private readonly ILogger<TasmotaWorker> _logger = logger!;
    private TasmotaClient? _client;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client = new TasmotaClient("10.0.4.41");

        _logger.LogInformation("TasmotaWorker started");

        // Example: ensure Timers subsystem is enabled and keep relay heartbeat
        await _client.EnableAllTimersAsync(true);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Heartbeat: toggle LED state mode 1..8 (or just read status)
                await _client.SetLedStateAsync(1);

                var wifi = await _client.GetWifiInfoAsync();
                _logger.LogInformation("WiFi: RSSI={Rssi} SSID={Ssid}",
                    wifi?.Wifi?.RSSI, wifi?.Wifi?.SSId);

                // Every hour, make sure timezone is correct (idempotent)
                await _client.SetTimezoneAsync(3);

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop error");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("TasmotaWorker stopping");
    }
}
```

`Program.cs`:

```csharp
// <Project Sdk="Microsoft.NET.Sdk.Worker">
//   <PropertyGroup>
//     <TargetFramework>net8.0</TargetFramework>
//     <Nullable>enable</Nullable>
//     <ImplicitUsings>enable</ImplicitUsings>
//   </PropertyGroup>
//   <ItemGroup>
//     <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
//     <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.0" />
//   </ItemGroup>
// </Project>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

await Host.CreateDefaultBuilder(args)
    .ConfigureLogging(b => b.AddConsole())
    .ConfigureServices(services =>
    {
        services.AddHostedService<TasmotaWorker>();
    })
    .RunConsoleAsync();
```

> You can promote `TasmotaClient` to a DI service if you refactor its `HttpClient` handling to accept an injected, shared instance.

---

## License

Use freely in your projects. Attribution appreciated. 💚

---

## Changelog (summary)

* Initial release: relays, time/zone, Wi-Fi, MQTT, Timers, Rules (one-shot / relative / sunrise-sunset), backup/restore, multi-relay scheduling (`SetTimerMultiAsync`).
