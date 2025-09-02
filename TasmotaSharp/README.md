# TasmotaSharp

A lightweight **HTTP-based Tasmota client** for .NET.
Wraps Tasmota’s `/cm?cmnd=` API with typed async methods for **relays**, **timers**, **rules**, **time/zone/DST**, **Wi-Fi**, **MQTT**, **sensors**, **geo**, **LEDs**, **backup/restore**, and more.

---

## Installation

Add via NuGet:

```bash
dotnet add package TasmotaSharp
```

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

### 5) Wi-Fi scan

```csharp
var nets = await client.ScanWifiAsync();
foreach (var ap in nets)
    Console.WriteLine($"{ap.SSID} {ap.SignalDbm} dBm");
```

### 6) MQTT setup

```csharp
await client.SetMqttAsync("10.0.4.10", 1883, "user", "pass", "client01", "tasmota/dev01");
var mqtt = await client.GetMqttStatusAsync();
Console.WriteLine($"Connected: {mqtt?.MqttHost}");
```

### 7) Backup/Restore

```csharp
var backup = await client.BackupConfigAsync();
await File.WriteAllBytesAsync("config.dmp", backup!);
var ok = await client.RestoreConfigAsync(backup!);
```

### 8) LED control

```csharp
await client.SetLedStateAsync(1);       // mode
await client.SetLedPowerAsync(1, true); // ON
```

### 9) System

```csharp
await client.RestartAsync();
await client.FactoryResetAsync(1);
```

---

## Example: Worker Service with DI

`TasmotaWorker.cs`:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TasmotaSharp;

public sealed class TasmotaWorker(ILogger<TasmotaWorker> logger, TasmotaClient client) : BackgroundService
{
    private readonly ILogger<TasmotaWorker> _logger = logger;
    private readonly TasmotaClient _client = client;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TasmotaWorker started");

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
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loop error");
                await Task.Delay(10000, stoppingToken);
            }
        }

        _logger.LogInformation("TasmotaWorker stopping");
    }
}
```

`Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TasmotaSharp;

await Host.CreateDefaultBuilder(args)
    .ConfigureLogging(b => b.AddConsole())
    .ConfigureServices(services =>
    {
        services.AddHttpClient();

        services.AddSingleton<TasmotaClient>(sp =>
        {
            var client = new TasmotaClient("10.0.4.41");
            return client;
        });
        //or 
        //services.AddSingleton<TasmotaClient>();
        //then use SetIp(string ipOrHost)
        services.AddHostedService<TasmotaWorker>();
    })
    .RunConsoleAsync();
```

---

## Tips & caveats

* All methods are **async**; always `await`.
* **Rules Delay** unit = deciseconds (1/10 s).
* **Timers:** max 16 entries. Multi uses 2 per relay (ON/OFF).
* **Rules:** only Rule1–Rule3. Helpers may consume 2 rules.
* **Day masks:** timers manage days better than rules.
* Wi-Fi scan may require polling (status: `"Scanning"`).
* Class owns its `HttpClient`. Use `IHttpClientFactory` for many devices.
* Plain HTTP; keep devices in trusted networks.

---

## License

MIT. Use freely in your projects. Attribution appreciated 💚

---

## Changelog

* v1.0: Initial release with relays, timers, rules, sensors, Wi-Fi, MQTT, geo, LED, backup/restore, mDNS, system functions.

---

## 🙌 Acknowledgements

* [Tasmota](https://tasmota.github.io/) project and community.