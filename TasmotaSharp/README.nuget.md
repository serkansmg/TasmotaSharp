
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

// Relay
await client.SetRelayAsync(1, true);
var state = await client.GetRelayStateAsync(1);

// Time & DST
await client.SetTimezoneAsync(3);        // UTC+3
await client.SetDstAsync(false);
await client.SetTimeAsync(DateTime.Now);

// Wi-Fi scan
var nets = await client.ScanWifiAsync();

// One-shot: turn relay1 ON for 30s at specific date
await client.SetOneShotDateRuleAsync(
    1,
    new DateTime(2025,9,5,18,30,0),
    1,
    true,
    TimeSpan.FromSeconds(30)
);

// Backup config
var backup = await client.BackupConfigAsync();
await client.RestoreConfigAsync(backup!);
```

---

## Highlights

* **Relays:** Set, toggle, query state
* **Timers:** Weekly, multi-relay, clear/disable
* **Rules:** One-shot, relative pulse, sunrise/sunset
* **Wi-Fi:** Set SSID/Password, scan, status
* **MQTT:** Configure, query status
* **Sensors:** Read (Status 10), set TelePeriod
* **System:** Restart, factory reset
* **Geo:** Latitude/Longitude
* **LED:** Mode + power control
* **Backup/Restore:** Config dump upload/download
* **mDNS:** Enable/disable, query state

---

## License

MIT © Serkan Polat
Use freely in your projects 💚

