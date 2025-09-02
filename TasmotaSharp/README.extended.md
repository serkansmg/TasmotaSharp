 

 
# TasmotaSharp

A lightweight **HTTP-based Tasmota client** for .NET.  
Wraps Tasmota’s `/cm?cmnd=` API with typed async methods for **relays**, **timers**, **rules**, **time/zone/DST**, **Wi-Fi**, **MQTT**, **sensors**, **geo**, **LEDs**, **backup/restore**, and more.

---

## Installation

Add via NuGet:

```bash
dotnet add package TasmotaSharp
````

Or copy the file and models:

```
TasmotaSharp/TasmotaClient.cs
```

**Requirements:** .NET 6+ (tested with .NET 8/9).
Dependencies: `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`.

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
await client.SetTimeAsync(DateTime.Now);  // sync time

// Sensors
var sns = await client.GetSensorStatusAsync();

// Weekly timers
await client.SetTimerAsync(1, new TimeSpan(18,30,0),
    new[]{ DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Saturday }, 1, 1); // ON
await client.SetTimerAsync(2, new TimeSpan(23,0,0),
    new[]{ DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Saturday }, 1, 0); // OFF

// Multi-relay schedule
await client.SetTimerMultiAsync(
    days: new[]{ DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday },
    onTimeHHmm: "09:00",
    offTimeHHmm:"22:00",
    outputs: new[]{1,2,3},
    strategy: MultiScheduleStrategy.Timers,
    startTimerIndex: 1
);

// One-shot at absolute time
await client.SetOneShotDateRuleAsync(1, new DateTime(2025,9,5,18,30,0), 1, true, TimeSpan.FromSeconds(30));

// Relative pulse “Now + 10s”
await client.ScheduleOneShotInAsync(TimeSpan.FromSeconds(10), 1, true, TimeSpan.FromSeconds(10));

// Rule read/delete
var r1 = await client.GetRuleInfoAsync(1);
await client.DeleteRuleAsync(1);

// Wi-Fi
await client.SetWifiCredentialsAsync("OfficeWiFi", "StrongPass!", restartAfter: true);
var wifi = await client.GetWifiInfoAsync();
var nets = await client.ScanWifiAsync();

// MQTT
await client.SetMqttAsync("10.0.4.10", port:1883, user:"smg", password:"secret");
var mqtt = await client.GetMqttStatusAsync();

// Backup/restore
var bytes = await client.BackupConfigAsync();
await client.RestoreConfigAsync(bytes);

// Restart
await client.RestartAsync();
```

---

## Feature highlights

* **Relays:** `SetRelayAsync`, `ToggleRelayAsync`, `GetRelayStateAsync`
* **Time/Timezone/DST:** `GetTimeAsync`, `SetTimeAsync`, `SetTimezoneAsync`, `SetDstAsync`
* **Status/Sensors:** `GetStatusAsync`, `GetSensorStatusAsync`, `SetTelePeriodAsync`
* **Wi-Fi:** `SetWifiCredentialsAsync`, `GetWifiInfoAsync`, `ScanWifiAsync`
* **MQTT:** `SetMqttAsync`, `GetMqttStatusAsync`
* **Scheduling**

  * **Timers (weekly):** `SetTimerAsync`, `SetTimerByMaskAsync`, `EnableAllTimersAsync`, `DisableTimerAsync`, `ClearTimerAsync`
  * **Rules:** `SetOneShotDateRuleAsync`, `ScheduleOneShotInAsync`, `PulseAfterAsync`, `SetRuleScriptAsync`, `EnableRuleAsync`, `GetRuleInfoAsync`, `GetAllRulesAsync`, `ClearRuleAsync`, `DeleteRuleAsync`
  * **Multi-relay orchestration:** `SetTimerMultiAsync`
  * **SimpleRule model:** `ApplySimpleRuleAsync`, `GetSimpleRuleAsync`
* **Geo:** `SetGeoAsync` (for Sunrise/Sunset rules)
* **LED:** `SetLedStateAsync`, `SetLedPowerAsync`
* **mDNS:** `EnableMdnsAsync`, `GetMdnsStateAsync`
* **System:** `RestartAsync`, `FactoryResetAsync`
* **Backup/Restore:** `BackupConfigAsync`, `RestoreConfigAsync`

---

## Real-world use cases

### 1) Toggle relay and check status

```csharp
var newState = await client.ToggleRelayAsync(1);
Console.WriteLine($"Relay1 is now {(newState==true?"ON":"OFF")}");
```

### 2) Weekdays 08:30–20:15 ON/OFF for relays 1–4

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

### 3) Turn ON 15 minutes after sunset for 5 minutes

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
await client.ApplySimpleRuleAsync(1, rule);
```

### 4) Fire a short pulse after 30 seconds

```csharp
await client.PulseAfterAsync(30, 1, 5);
```

### 5) Absolute one-shot

```csharp
await client.SetOneShotDateRuleAsync(1, new DateTime(2025,9,5,18,30,0), 1, true, TimeSpan.FromSeconds(30));
```

### 6) Wi-Fi scan

```csharp
var nets = await client.ScanWifiAsync();
foreach (var ap in nets)
    Console.WriteLine($"{ap.SSID} {ap.SignalDbm} dBm");
```

### 7) MQTT setup

```csharp
await client.SetMqttAsync("10.0.4.10", 1883, "user", "pass", "client01", "tasmota/dev01");
var mqtt = await client.GetMqttStatusAsync();
Console.WriteLine($"Connected: {mqtt?.MqttHost}");
```

### 8) Backup/Restore

```csharp
var backup = await client.BackupConfigAsync();
await File.WriteAllBytesAsync("config.dmp", backup!);
var ok = await client.RestoreConfigAsync(backup!);
```

### 9) LED control

```csharp
await client.SetLedStateAsync(1);       // mode
await client.SetLedPowerAsync(1, true); // ON
```

### 10) System

```csharp
await client.RestartAsync();
await client.FactoryResetAsync(1);
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
* `Task<bool> SetTimezoneAsync(int offset)`
* `Task<bool> SetDstAsync(bool enabled)`

### Status & Sensors

* `Task<TasmotaStatus?> GetStatusAsync()`
* `Task<TasmotaSensorStatus?> GetSensorStatusAsync()`
* `Task<bool> SetTelePeriodAsync(int seconds)`

### Wi-Fi

* `Task<bool> SetWifiCredentialsAsync(string ssid, string password, bool restartAfter=true)`
* `Task<(StatusNET? Net, Wifi? Wifi)?> GetWifiInfoAsync()`
* `Task<List<WifiScanResult>?> ScanWifiAsync(int timeoutMs=5000, int pollIntervalMs=400)`

### MQTT

* `Task<bool> SetMqttAsync(...)`
* `Task<StatusMQT?> GetMqttStatusAsync()`

### Scheduling – Timers

* `Task<bool> SetTimerAsync(...)`
* `Task<bool> SetTimerByMaskAsync(...)`
* `Task<bool> EnableAllTimersAsync(bool enable)`
* `Task<bool> DisableTimerAsync(int index)`
* `Task<bool> ClearTimerAsync(int index)`
* `Task<bool> SetTimerMultiAsync(...)`

### Scheduling – Rules

* `Task<bool> SetOneShotDateRuleAsync(...)`
* `Task<bool> ScheduleOneShotInAsync(...)`
* `Task<bool> PulseAfterAsync(...)`
* `Task<bool> EnableRuleAsync(...)`
* `Task<bool> SetRuleScriptAsync(...)`
* `Task<bool> ClearRuleAsync(...)` / `DeleteRuleAsync(...)`
* `Task<RuleInfo?> GetRuleInfoAsync(int ruleIndex)`
* `Task<List<RuleInfo>> GetAllRulesAsync()`

### Simple rule model

* `Task<bool> ApplySimpleRuleAsync(...)`
* `Task<SimpleRule?> GetSimpleRuleAsync(int ruleIndex)`

### Geo / LED / mDNS / System / Backup

* `Task<bool> SetGeoAsync(double latitude, double longitude)`
* `Task<bool> SetLedStateAsync(int mode)`
* `Task<bool?> SetLedPowerAsync(int index, bool on)`
* `Task<bool> EnableMdnsAsync(bool enable)`
* `Task<bool?> GetMdnsStateAsync()`
* `Task<bool> RestartAsync()`
* `Task<bool> FactoryResetAsync(int mode)`
* `Task<byte[]?> BackupConfigAsync()`
* `Task<bool> RestoreConfigAsync(byte[] dmpData)`

---

## Tips & caveats

* All methods are **async**; always `await`.
* **Rules Delay** unit = deciseconds (1/10 s).
* **Timers:** max 16 entries. Multi uses 2 per relay (ON/OFF).
* **Rules:** only Rule1–Rule3. Helpers may consume 2 rules.
* **Day masks:** timers manage days better than rules.
* Wi-Fi scan may require polling (status: "Scanning").
* Class owns its `HttpClient`. Use `IHttpClientFactory` for many devices.
* Plain HTTP; keep devices in trusted networks.

---

## Troubleshooting

**Rule didn’t fire at expected time**

* Check device time (`GetTimeAsync` / `SetTimeAsync`).
* Ensure rule is enabled (`EnableRuleAsync`).

**Wi-Fi scan empty**

* Device reports `"Scanning"` until complete. Increase timeout.

**Timer slots full**

* Max 16. Use RuleBacklog or split across devices.

---

## Example: Console App

```csharp
static async Task<int> Main(string[] args)
{
    var host = args.Length > 0 ? args[0] : "10.0.4.41";
    var client = new TasmotaClient(host);

    Console.WriteLine($"Connected to {host}");

    var state = await client.ToggleRelayAsync(1);
    Console.WriteLine($"Relay1: {(state==true?"ON":"OFF")}");

    await client.ScheduleOneShotInAsync(TimeSpan.FromSeconds(10), 1, true, TimeSpan.FromSeconds(10));

    var nets = await client.ScanWifiAsync();
    foreach (var ap in nets ?? [])
        Console.WriteLine($"{ap.SSID} ch{ap.Channel} {ap.SignalDbm} dBm");
    return 0;
}
```

---

## Example: Worker Service with DI

```csharp
public sealed class TasmotaWorker(ILogger<TasmotaWorker> logger) : BackgroundService
{
    private readonly ILogger<TasmotaWorker> _logger = logger;
    private readonly TasmotaClient _client = new("10.0.4.41");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started");
        await _client.EnableAllTimersAsync(true);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _client.SetLedStateAsync(1);
                var wifi = await _client.GetWifiInfoAsync();
                _logger.LogInformation("WiFi: RSSI={r}, SSID={s}", wifi?.Wifi?.RSSI, wifi?.Wifi?.SSId);

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loop error");
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}
```

---

## License

MIT. Use freely in your projects. Attribution appreciated 💚

---

## Changelog

* v1.0: Initial release with relays, timers, rules, sensors, Wi-Fi, MQTT, geo, LED, backup/restore, mDNS, system functions.

```

