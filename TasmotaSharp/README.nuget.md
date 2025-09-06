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

## Highlights

* **Device Discovery:** Auto-detect relay count and capabilities
* **Relays:** Set, toggle, query state with multi-relay support
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

## What's New in v1.0.3

* **Device Capabilities:** `GetRelayCountAsync()` for automatic relay detection
* **Access Point Management:** Full AP mode configuration and control
* **Wi-Fi Recovery:** Configurable strategies for connection failures
* **Enhanced Error Handling:** Better logging and exception management
* **Improved Documentation:** Comprehensive examples and use cases

---

## License

MIT © Serkan Polat
Use freely in your projects 💚