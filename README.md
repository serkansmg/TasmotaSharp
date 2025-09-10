# TasmotaSharp

A lightweight **HTTP-based Tasmota client** for .NET.
Wraps Tasmota's `/cm?cmnd=` API with typed async methods for **relays**, **timers**, **rules**, **time/zone/DST**, **Wi-Fi**, **MQTT**, **sensors**, **geo**, **LEDs**, **backup/restore**, and more.

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

// Relay control
await client.SetRelayAsync(1, true);      // Turn relay 1 ON
bool? isOn = await client.GetRelayStateAsync(1);

// Multi-relay control (NEW!)
await client.SetMultipleRelaysAsync(new Dictionary<int, bool> {
    { 1, true }, { 2, false }, { 3, true }  // Set different states per relay
});

await client.SetRelayGroupAsync(new[] { 1, 2, 3 }, true);   // Set multiple relays to same state
await client.SetAllRelaysAsync(false);                      // Turn OFF all relays
await client.SetRelaysSequentialAsync(new[] { 1, 2, 3, 4 }, true, 200); // Sequential ON with 200ms delay

// Get total relay count
int? relayCount = await client.GetRelayCountAsync();

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

// Relative pulse – "Now + 10s"
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

* **Relays:** `SetRelayAsync`, `ToggleRelayAsync`, `GetRelayStateAsync`, `GetRelayCountAsync`
* **Multi-Relay Control:** `SetMultipleRelaysAsync`, `SetRelayGroupAsync`, `SetAllRelaysAsync`, `SetRelaysSequentialAsync`
* **Time/Timezone/DST:** `GetTimeAsync`, `SetTimeAsync`, `SetTimezoneAsync`, `SetDstAsync`
* **Status/Sensors:** `GetStatusAsync`, `GetSensorStatusAsync`, `SetTelePeriodAsync`
* **Wi-Fi:** `SetWifiCredentialsAsync`, `GetWifiInfoAsync`, `ScanWifiAsync`
* **Advanced Wi-Fi:** `SetAccessPointModeAsync`, `SetAccessPointCredentialsAsync`, `GetWifiModeAsync`, `SetWifiRecoveryModeAsync`
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

### 1) Multi-relay control strategies (NEW!)

```csharp
// Set multiple relays to different states in one command
var relayStates = new Dictionary<int, bool> 
{
    { 1, true },   // Relay 1 ON
    { 2, false },  // Relay 2 OFF
    { 3, true },   // Relay 3 ON
    { 4, true }    // Relay 4 ON
};
await client.SetMultipleRelaysAsync(relayStates);

// Turn specific relays ON together
await client.SetRelayGroupAsync(new[] { 1, 3, 5 }, true);

// Emergency shutdown - turn all relays OFF
await client.SetAllRelaysAsync(false);

// Sequential startup with delay (e.g., motor startup sequence)
await client.SetRelaysSequentialAsync(new[] { 1, 2, 3, 4 }, true, 500); // 500ms delay between each

// Staggered shutdown
await client.SetRelaysSequentialAsync(new[] { 4, 3, 2, 1 }, false, 300); // Reverse order
```

### 2) Smart home automation scenarios

```csharp
// Morning routine: gradually turn on different areas
await client.SetRelaysSequentialAsync(new[] { 1, 2, 3 }, true, 1000); // 1 second between each

// Evening shutdown: turn off non-essential devices first, then security
await client.SetRelayGroupAsync(new[] { 2, 3, 4 }, false);  // Turn off lights/appliances
await Task.Delay(2000);
await client.SetRelayAsync(1, false);  // Turn off main power last

// Scene control: living room movie mode
await client.SetMultipleRelaysAsync(new Dictionary<int, bool> {
    { 1, false },  // Main lights OFF
    { 2, true },   // Ambient lights ON
    { 3, true },   // TV and sound system ON
    { 4, false }   // AC reduced
});
```

### 3) Industrial control patterns

```csharp
// Motor startup sequence with safety delays
await client.SetRelayAsync(1, true);      // Enable safety systems first
await Task.Delay(2000);
await client.SetRelaysSequentialAsync(new[] { 2, 3, 4 }, true, 1500); // Start motors with delay

// Emergency stop - immediate shutdown of all operational relays
await client.SetRelayGroupAsync(new[] { 2, 3, 4, 5, 6 }, false);

// Production line control
var productionRelays = new Dictionary<int, bool> {
    { 1, true },   // Conveyor belt
    { 2, true },   // Processing unit
    { 3, false },  // Quality control (manual mode)
    { 4, true }    // Packaging unit
};
await client.SetMultipleRelaysAsync(productionRelays);
```

### 4) Check relay count and toggle states

```csharp
// Discover how many relays the device has
var relayCount = await client.GetRelayCountAsync();
Console.WriteLine($"Device has {relayCount} relays");

// Toggle each relay and display new state
for (int i = 1; i <= relayCount; i++)
{
    var newState = await client.ToggleRelayAsync(i);
    Console.WriteLine($"Relay{i} is now {(newState==true?"ON":"OFF")}");
}
```

### 5) Weekdays 08:30–20:15 ON/OFF for relays 1–4

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

### 6) Turn ON 15 minutes after sunset for 5 minutes

```csharp
await client.SetGeoAsync(41.0082, 28.9784); // Istanbul coordinates
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

### 7) Fire a short pulse after 30 seconds

```csharp
// Turn relay 1 ON after 30 seconds, then OFF after 5 more seconds
await client.PulseAfterAsync(30, 1, 5);
```

### 8) Wi-Fi management and scanning

```csharp
// Scan for available networks
var nets = await client.ScanWifiAsync();
foreach (var ap in nets)
    Console.WriteLine($"{ap.SSID} {ap.SignalDbm} dBm, Channel {ap.Channel}");

// Check current Wi-Fi mode
var mode = await client.GetWifiModeAsync();
Console.WriteLine($"Current Wi-Fi mode: {mode}");

// Set up as Access Point
await client.SetAccessPointModeAsync(true);
await client.SetAccessPointCredentialsAsync("MyTasmota", "password123");

// Set Wi-Fi recovery mode (what happens when connection fails)
await client.SetWifiRecoveryModeAsync(WifiRecoveryMode.SmartConfig);
```

### 9) MQTT setup and monitoring

```csharp
await client.SetMqttAsync("10.0.4.10", 1883, "user", "pass", "client01", "tasmota/dev01");
var mqtt = await client.GetMqttStatusAsync();
Console.WriteLine($"MQTT Connected: {mqtt?.MqttHost}, Count: {mqtt?.MqttCount}");
```

### 10) Configuration backup and restore

```csharp
// Create a backup
var backup = await client.BackupConfigAsync();
await File.WriteAllBytesAsync("tasmota-config.dmp", backup!);

// Restore from backup
var backupData = await File.ReadAllBytesAsync("tasmota-config.dmp");
var success = await client.RestoreConfigAsync(backupData);
Console.WriteLine($"Restore {(success ? "successful" : "failed")}");
```

### 11) LED control and indication

```csharp
// Set LED to follow relay state
await client.SetLedStateAsync(1);

// Turn LED power ON
await client.SetLedPowerAsync(1, true);

// Blink pattern for status indication
for (int i = 0; i < 5; i++)
{
    await client.SetLedPowerAsync(1, true);
    await Task.Delay(200);
    await client.SetLedPowerAsync(1, false);
    await Task.Delay(200);
}
```

### 12) Time synchronization and timezone management

```csharp
// Set timezone and disable DST
await client.SetTimezoneAsync(3); // UTC+3 for Turkey
await client.SetDstAsync(false);

// Sync device time with system time
await client.SetTimeAsync(DateTime.Now);

// Read back the device time
var deviceTime = await client.GetTimeAsync();
Console.WriteLine($"Device time: {deviceTime}");
```

### 13) Complex scheduling with multiple strategies

```csharp
// Office hours schedule using timers (best for day-specific control)
await client.SetTimerMultiAsync(
    days: new[]{ DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
    onTimeHHmm: "08:00",
    offTimeHHmm: "18:00",
    outputs: new[]{1, 2, 3},
    strategy: MultiScheduleStrategy.Timers
);

// Security lights using rule-based approach (simpler minute-based triggers)
await client.SetTimerMultiAsync(
    days: new[]{ DayOfWeek.Saturday, DayOfWeek.Sunday },
    onTimeHHmm: "20:00",
    offTimeHHmm: "06:00",
    outputs: new[]{4, 5},
    strategy: MultiScheduleStrategy.RuleBacklog,
    ruleIndex: 2
);
```

### 14) Sensor monitoring and telemetry

```csharp
// Read sensor data (requires sensors build)
var sensors = await client.GetSensorStatusAsync();
if (sensors?.StatusSNS?.DS3231 != null)
{
    var rtc = sensors.StatusSNS.DS3231;
    Console.WriteLine($"RTC Temperature: {rtc.Temperature}°C");
    Console.WriteLine($"RTC Time: {rtc.Time}");
}

// Set telemetry reporting interval to 30 seconds
await client.SetTelePeriodAsync(30);
```

### 15) System maintenance and diagnostics

```csharp
// Enable mDNS for easier discovery
await client.EnableMdnsAsync(true);
var mdnsEnabled = await client.GetMdnsStateAsync();
Console.WriteLine($"mDNS enabled: {mdnsEnabled}");

// Get full device status
var status = await client.GetStatusAsync();
Console.WriteLine($"Device: {status?.Status?.FriendlyName}");
Console.WriteLine($"Uptime: {status?.StatusSTS?.UptimeSec} seconds");
Console.WriteLine($"Free heap: {status?.StatusSTS?.Heap} bytes");

// Perform maintenance restart
await client.RestartAsync();
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

        // Initialize: enable all timers and check relay count
        await _client.EnableAllTimersAsync(true);
        var relayCount = await _client.GetRelayCountAsync();
        _logger.LogInformation("Device has {RelayCount} relays", relayCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Update LED state
                await _client.SetLedStateAsync(1);
                
                // Monitor Wi-Fi connection
                var wifi = await _client.GetWifiInfoAsync();
                _logger.LogInformation("WiFi: RSSI={RSSI}dBm, SSID={SSID}", 
                    wifi?.Wifi?.RSSI, wifi?.Wifi?.SSId);

                // Check MQTT status
                var mqtt = await _client.GetMqttStatusAsync();
                if (mqtt != null)
                {
                    _logger.LogInformation("MQTT: Host={Host}, Count={Count}", 
                        mqtt.MqttHost, mqtt.MqttCount);
                }

                // Monitor sensors if available
                var sensors = await _client.GetSensorStatusAsync();
                if (sensors?.StatusSNS?.DS3231 != null)
                {
                    _logger.LogInformation("RTC Temp: {Temp}°C", 
                        sensors.StatusSNS.DS3231.Temperature);
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop error");
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
            var logger = sp.GetService<ILogger<TasmotaClient>>();
            var httpFactory = sp.GetService<IHttpClientFactory>();
            var client = new TasmotaClient("10.0.4.41", logger, httpFactory);
            return client;
        });
        
        // Alternative: parameterless constructor with SetIp
        // services.AddSingleton<TasmotaClient>();
        // then use SetIp(string ipOrHost) method
        
        services.AddHostedService<TasmotaWorker>();
    })
    .RunConsoleAsync();
```

---

## Advanced Wi-Fi Configuration

### Access Point Mode Setup

```csharp
// Enable Access Point mode
await client.SetAccessPointModeAsync(true);

// Configure AP credentials
await client.SetAccessPointCredentialsAsync("MyTasmotaAP", "SecurePassword123");

// Check current Wi-Fi configuration mode
var mode = await client.GetWifiModeAsync();
Console.WriteLine($"Wi-Fi Mode: {mode}");
```

### Wi-Fi Recovery Strategies

```csharp
// Set different recovery modes for connection failures
await client.SetWifiRecoveryModeAsync(WifiRecoveryMode.RetryOtherAP);  // Try other saved networks
await client.SetWifiRecoveryModeAsync(WifiRecoveryMode.RestartThenAP); // Restart then enable AP
await client.SetWifiRecoveryModeAsync(WifiRecoveryMode.SmartConfig);    // Enable SmartConfig
```

---

## Tips & caveats

* All methods are **async**; always `await`.
* **Rules Delay** unit = deciseconds (1/10 s).
* **Timers:** max 16 entries. Multi uses 2 per relay (ON/OFF).
* **Rules:** only Rule1–Rule3. Helpers may consume 2 rules.
* **Day masks:** timers manage days better than rules.
* **Relay detection:** Use `GetRelayCountAsync()` to auto-discover device capabilities.
* **Multi-relay control:** `SetMultipleRelaysAsync()` allows different states per relay, while `SetRelayGroupAsync()` sets the same state for multiple relays.
* **Sequential control:** `SetRelaysSequentialAsync()` is useful for motor startup sequences or gradual lighting control.
* **Wi-Fi modes:** Different devices support different Wi-Fi recovery strategies.
* Wi-Fi scan may require polling (status: `"Scanning"`).
* Class owns its `HttpClient`. Use `IHttpClientFactory` for many devices.
* Plain HTTP; keep devices in trusted networks.

---

## License

MIT. Use freely in your projects. Attribution appreciated 💚

---

## Changelog

* **v1.0.4:** Added multi-relay batch control methods: `SetMultipleRelaysAsync()`, `SetRelayGroupAsync()`, `SetAllRelaysAsync()`, `SetRelaysSequentialAsync()`. Enhanced documentation with industrial and smart home automation examples.
* **v1.0.3:** Added `GetRelayCountAsync()`, Wi-Fi Access Point management (`SetAccessPointModeAsync`, `SetAccessPointCredentialsAsync`), Wi-Fi mode detection (`GetWifiModeAsync`), Wi-Fi recovery mode configuration (`SetWifiRecoveryModeAsync`), improved error handling and logging.
* **v1.0.2:** Enhanced multi-relay scheduling, improved Wi-Fi scan reliability, better JSON parsing.
* **v1.0.1:** Added SimpleRule model, sunrise/sunset support, improved timer management.
* **v1.0:** Initial release with relays, timers, rules, sensors, Wi-Fi, MQTT, geo, LED, backup/restore, mDNS, system functions.

---

## 🙌 Acknowledgements

* [Tasmota](https://tasmota.github.io/) project and community.
* [TasmotaMobileClient Blazor Maui Mobile App](https://github.com/serkansmg/TasmotaMobileClient/) by Serkan Polat.