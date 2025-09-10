# TasmotaSharp
[![NuGet Version](https://img.shields.io/nuget/v/TasmotaSharp.svg?style=flat-square)](https://www.nuget.org/packages/TasmotaSharp)
[![NuGet Downloads](https://img.shields.io/nuget/dt/TasmotaSharp.svg?style=flat-square)](https://www.nuget.org/packages/TasmotaSharp)

A lightweight **HTTP-based Tasmota client** for .NET.  
Provides typed async methods for **relays, timers, rules, Wi-Fi, MQTT, sensors, backup/restore, geo, LEDs, mDNS, system**.

---

## Install

```bash
dotnet add package TasmotaSharp
```

Requirements: .NET 6 or higher (tested with .NET 8 and .NET 9).

---

## Usage

```csharp
using TasmotaSharp;

var client = new TasmotaClient("10.0.4.41");

// Relay control
await client.SetRelayAsync(1, true);          // Turn ON
var state = await client.GetRelayStateAsync(1);
var relayCount = await client.GetRelayCountAsync(); // Auto-discover device capabilities

// Multi-relay control (NEW!)
await client.SetMultipleRelaysAsync(new Dictionary<int, bool> {
    { 1, true }, { 2, false }, { 3, true }      // Different states per relay
});
await client.SetRelayGroupAsync(new[] { 1, 2, 3 }, true);   // Same state for multiple relays
await client.SetAllRelaysAsync(false);                      // Turn OFF all relays
await client.SetRelaysSequentialAsync(new[] { 1, 2, 3 }, true, 200); // Sequential with delay

// Time & timezone management
await client.SetTimezoneAsync(3);             // UTC+3
await client.SetDstAsync(false);
await client.SetTimeAsync(DateTime.Now);

// Wi-Fi management
var nets = await client.ScanWifiAsync();
await client.SetWifiCredentialsAsync("MyWiFi", "password");

// Access Point mode
await client.SetAccessPointModeAsync(true);
await client.SetAccessPointCredentialsAsync("TasmotaAP", "securepass");
var mode = await client.GetWifiModeAsync();

// Wi-Fi recovery strategies
await client.SetWifiRecoveryModeAsync(WifiRecoveryMode.SmartConfig);

// One-shot scheduling: turn relay1 ON for 30s at specific date
await client.SetOneShotDateRuleAsync(
    1,
    new DateTime(2025,9,5,18,30,0),
    1,
    true,
    TimeSpan.FromSeconds(30)
);

// Multi-relay schedule (office hours)
await client.SetTimerMultiAsync(
    days: new[]{ DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
    onTimeHHmm: "08:30",
    offTimeHHmm: "17:30",
    outputs: new[]{1, 2, 3},
    strategy: MultiScheduleStrategy.Timers
);

// LED state control
await client.SetLedStateAsync(1);             // Set LED mode
await client.SetLedPowerAsync(1, true);       // Turn LED ON

// System monitoring
var status = await client.GetStatusAsync();
var sensors = await client.GetSensorStatusAsync();

// Configuration backup/restore
var backup = await client.BackupConfigAsync();
await client.RestoreConfigAsync(backup!);

// System management
await client.RestartAsync();
await client.EnableMdnsAsync(true);
```

---

## Multi-Relay Control Examples

### Smart Home Automation
```csharp
// Living room scene control
await client.SetMultipleRelaysAsync(new Dictionary<int, bool> {
    { 1, false },  // Main lights OFF
    { 2, true },   // Ambient lights ON
    { 3, true },   // Entertainment system ON
    { 4, false }   // AC to low
});

// Morning routine: gradual activation
await client.SetRelaysSequentialAsync(new[] { 1, 2, 3 }, true, 1000); // 1-second intervals
```

### Industrial Control
```csharp
// Motor startup sequence with safety delays
await client.SetRelayAsync(1, true);      // Safety systems first
await Task.Delay(2000);
await client.SetRelaysSequentialAsync(new[] { 2, 3, 4 }, true, 1500); // Motors with delay

// Emergency stop - immediate shutdown
await client.SetRelayGroupAsync(new[] { 2, 3, 4, 5 }, false);
```

### Energy Management
```csharp
// Load shedding: turn off non-essential devices
await client.SetRelayGroupAsync(new[] { 3, 4, 5 }, false);  // Non-essential OFF
await Task.Delay(5000);
await client.SetRelayGroupAsync(new[] { 1, 2 }, true);      // Essential systems ON
```

---

## Highlights

* **Device Discovery:** Auto-detect relay count and capabilities
* **Relays:** Set, toggle, query state with multi-relay support
* **Multi-Relay Control:** Batch operations for efficient device management
    * `SetMultipleRelaysAsync()` - Different states per relay
    * `SetRelayGroupAsync()` - Same state for multiple relays
    * `SetAllRelaysAsync()` - Control all relays at once
    * `SetRelaysSequentialAsync()` - Sequential control with delays
* **Advanced Wi-Fi:** Access Point mode, recovery strategies, scanning
* **Timers:** Weekly schedules, multi-relay orchestration, clear/disable
* **Rules:** One-shot, relative pulse, sunrise/sunset automation
* **Sensors:** Read telemetry (DS3231, etc.), configure reporting
* **MQTT:** Full configuration and status monitoring
* **System:** Restart, factory reset, mDNS control
* **LED:** Mode and power control for status indication
* **Geo:** Latitude/longitude for sunrise/sunset rules
* **Backup/Restore:** Complete configuration management
* **Time Management:** Timezone, DST, clock synchronization

---

## What's New in v1.0.4

* **Multi-Relay Batch Control:** Four new methods for efficient relay management
    * Individual states: `SetMultipleRelaysAsync()`
    * Group control: `SetRelayGroupAsync()`
    * All relays: `SetAllRelaysAsync()`
    * Sequential timing: `SetRelaysSequentialAsync()`
* **Enhanced Examples:** Smart home, industrial, and energy management scenarios
* **Performance Optimization:** Single-command batch operations reduce HTTP overhead
* **Better Documentation:** Comprehensive use cases and patterns

---

## Previous Updates

* **v1.0.3:** Device capability discovery, Access Point management, Wi-Fi recovery modes
* **v1.0.2:** Enhanced multi-relay scheduling, improved Wi-Fi scan reliability
* **v1.0.1:** SimpleRule model, sunrise/sunset support, improved timer management

---

## License

MIT © Serkan Polat
Use freely in your projects 💚


## 🙌 Acknowledgements

* [Tasmota](https://tasmota.github.io/) project and community.
* [TasmotaMobileClient Blazor Maui Mobile App](https://github.com/serkansmg/TasmotaMobileClient/) by Serkan Polat.