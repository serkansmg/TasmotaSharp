# TasmotaSharp

Lightweight **C# HTTP client** for Tasmota devices (ESP32/ESP8266) using the REST API (`/cm?cmnd=`).
Provides convenient wrappers for **relays, time/zone, Wi-Fi, MQTT, timers, rules, sensors, config backup/restore, mDNS**, and a **multi-relay scheduler**.

---

## ✨ Features

* 🔌 Relay control (Power1..Power8)
* 🕒 Time/Timezone/DST + DS3231 (All Sensors build)
* 🌐 Wi-Fi set/get/scan
* 🔔 MQTT configuration & status
* 📅 Timers (weekly schedules) with day masks
* 🧠 Rules: one-shot at date/time, relative pulses, sunrise/sunset, helpers
* 💾 Config backup/restore (`/dl`, `/u`)
* 🔄 Restart & Factory reset (Reset 1/2/5)
* 📣 mDNS enable/disable (SetOption55)
* 💡 LED helpers
* 👥 Multi-relay scheduler (Timers or Rule+Backlog strategies)

---

## 📦 Installation

Add the project/class to your solution or pack it into a NuGet you control.

```bash
dotnet add package TasmotaSharp  
```

> Targets: .NET 6+ recommended.

---

## 🚀 Quick Start

```csharp
using TasmotaSharp;

// connect to device (IP or mDNS host like "tasmota-xxxx.local")
var client = new TasmotaClient("10.0.4.41");

// Relay control
await client.SetRelayAsync(1, true);              // Relay1 ON
var isOn = await client.GetRelayStateAsync(1);    // read state

// Time & timezone
await client.SetTimezoneAsync(3);                 // UTC+3
await client.SetDstAsync(false);                  // DST off
await client.SetTimeAsync(DateTime.Now);          // sync device time

// Sensors (requires All Sensors build; DS3231 supported)
var sns = await client.GetSensorStatusAsync();    // Status 10

// Timers (weekly)
await client.SetTimerAsync(
  index: 1,
  time: new TimeSpan(18,30,0),
  days: new[]{ DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Saturday },
  output: 1, action: 1);                          // 18:30 ON

await client.SetTimerAsync(
  index: 2,
  time: new TimeSpan(23,0,0),
  days: new[]{ DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Saturday },
  output: 1, action: 0);                          // 23:00 OFF

// One-shot at specific local time: 2025-09-05 18:30 -> ON then 30s later OFF
await client.SetOneShotDateRuleAsync(1, new DateTime(2025,9,5,18,30,0), 1, true, TimeSpan.FromSeconds(30));

// Relative pulse: now+10s ON, then OFF after 10s
await client.ScheduleOneShotInAsync(TimeSpan.FromSeconds(10), 1, true, TimeSpan.FromSeconds(10));

// Rule management
var r1 = await client.GetRuleInfoAsync(1);
await client.DeleteRuleAsync(1);

// Wi-Fi scan
var nets = await client.ScanWifiAsync();

// Config backup/restore
var dump = await client.BackupConfigAsync();      // save .dmp
await client.RestoreConfigAsync(dump);            // restore
```

---

## 📚 API Overview

### Construction

```csharp
var client = new TasmotaClient("10.0.4.41");   // or "tasmota-xxxx.local"
```

### Relay

```csharp
await client.SetRelayAsync(1, true);      // ON
await client.ToggleRelayAsync(1);
bool? state = await client.GetRelayStateAsync(1);
```

### Time / Zone / DST

```csharp
var now = await client.GetTimeAsync();
await client.SetTimeAsync(DateTime.Now);
await client.SetTimezoneAsync(3);         // UTC+3
await client.SetDstAsync(false);          // disable daylight saving
```

### Status & Sensors

```csharp
var st  = await client.GetStatusAsync();          // Status 0
var sns = await client.GetSensorStatusAsync();    // Status 10 (DS3231 etc.)
await client.SetTelePeriodAsync(60);
```

### Wi-Fi

```csharp
await client.SetWifiCredentialsAsync("MySSID", "MyPass", restartAfter: true);
var wifi = await client.GetWifiInfoAsync();       // (StatusNET, Wifi)
var aps  = await client.ScanWifiAsync();          // WifiScan 1 + poll
```

### MQTT

```csharp
await client.SetMqttAsync(host: "10.0.0.5", port:1883, user:"u", password:"p",
                          clientId:"tasmota-1", topic:"tasmota_1",
                          fullTopic:"%prefix%/%topic%/");
var mqt = await client.GetMqttStatusAsync();
```

### Config / System

```csharp
var cfg = await client.BackupConfigAsync();
await client.RestoreConfigAsync(cfg);

await client.RestartAsync();
await client.FactoryResetAsync(5);    // 1/2/5 (5 = safest full reset)
```

### mDNS

```csharp
await client.EnableMdnsAsync(true);
bool? m = await client.GetMdnsStateAsync();   // true/false/null
```

### LED

```csharp
await client.SetLedStateAsync(1);     // 0..8 (Tasmota dependent)
await client.SetLedPowerAsync(1, true);
```

### Timers (Weekly Schedules)

> `index` = 1..16, `action`: 0=OFF, 1=ON, 2=TOGGLE, 3=Rule

```csharp
await client.SetTimerAsync(
  index: 3,
  time: new TimeSpan(9,0,0),              // 09:00
  days: new[]{ DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday },
  output: 1,
  action: 1);                             // ON

await client.SetTimerByMaskAsync(4, "1-1-1--", "22:00", 1, 0); // mask Sun..Sat
await client.EnableAllTimersAsync(true);
await client.ClearTimerAsync(3);
```

#### Multi-Relay Weekly Scheduler

Two strategies:

* **Timers** (recommended): true weekly support; uses 2 timer slots per relay (ON/OFF).
* **RuleBacklog**: single rule with `Backlog PowerX ...` at specific minutes (easier to group many relays; day filtering requires more complex rules).

```csharp
// Pzt-Sal-Çar 09:00 AÇ, 22:00 KAPAT -> röle 1 ve 2 (Timers)
await client.SetTimerMultiAsync(
  days: new[]{ DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday },
  onTimeHHmm:  "09:00",
  offTimeHHmm: "22:00",
  outputs: new[]{ 1, 2 },
  strategy: MultiScheduleStrategy.Timers,
  startTimerIndex: 1
);

// Aynı işi tek rule ile (gün kısıtları basit değil): her gün
await client.SetTimerMultiAsync(
  days: new[]{ DayOfWeek.Monday, DayOfWeek.Tuesday }, // not: RuleBacklog günleri yok sayar (basit sürüm)
  onTimeHHmm:  "09:00",
  offTimeHHmm: "22:00",
  outputs: new[]{ 1, 2, 3 },
  strategy: MultiScheduleStrategy.RuleBacklog,
  ruleIndex: 1
);
```

### Rules (One-Shot / Relative / Sunrise/Sunset)

* **One-Shot absolute local time**
  Triggers once at `whenLocal`; optional pulse (returns to previous state).

```csharp
await client.SetOneShotDateRuleAsync(
  ruleIndex: 1,
  whenLocal: new DateTime(2025, 9, 5, 18, 30, 0),
  output: 1,
  onWhenTrue: true,
  pulse: TimeSpan.FromSeconds(30) // optional
);
```

* **Relative pulse** (now + delay)

```csharp
// 10s sonra ON, +10s sonra OFF
await client.ScheduleOneShotInAsync(TimeSpan.FromSeconds(10), 1, true, TimeSpan.FromSeconds(10));
```

* **Manage rules**

```csharp
var info = await client.GetRuleInfoAsync(1);
var all  = await client.GetAllRulesAsync();
await client.SetRuleScriptAsync(1, "on Time#Minute=540 do Power1 1 endon");
await client.EnableRuleAsync(1, true);
await client.DeleteRuleAsync(1);      // disable + clear
```

* **Relative pulse via RuleTimer**

```csharp
await client.PulseAfterAsync(
  startDelaySeconds: 30,   // 30s sonra
  output: 1,
  pulseSeconds: 10,        // 10s açık kal, sonra kapat
  ruleIndex: 1,
  timerIndex: 1
);
```

* **Sunrise/Sunset** (requires `SetGeoAsync(lat, lon)`)

```csharp
await client.SetGeoAsync(41.015137, 28.979530); // Istanbul
// Use ApplySimpleRuleAsync(...) with a SimpleRule of type SunriseSunset if you keep that helper.
```

---

## 🧩 Models (Overview)

* `TasmotaStatus` → wraps `Status`, `StatusPRM`, `StatusFWR`, `StatusNET`, `StatusSTS`, `StatusTIM`, `StatusMQT`, `StatusSNS`, etc.
  Types are **loose** (`JsonNumberHandling.AllowReadingFromString`) to handle Tasmota’s mixed numeric/string fields.

* `WifiScanResult` → SSID, BSSId, Channel, SignalDbm, RssiPercent, Encryption

* `RuleInfo` → Index, Enabled, Once, StopOnError, Free, Length, Script

*(Ensure these model classes are included in your project—matching what you’ve already implemented.)*

---

## ⚠️ Notes & Limits

* **Timers**: 16 slots total. Each relay needs **two** (ON/OFF).
* **Rules**: Only **3** slots (Rule1–Rule3). Use `Backlog` to control multiple outputs in one rule.
* **Delay** in Rules is **deciseconds** (1/10 sec).
* Use **Timers** for weekly plans (native day masks).
  Use **Rules** for **one-shot**, **relative**, or **complex** scenarios.
* For sunrise/sunset, set **Latitude/Longitude**.
* For DS3231, use **All Sensors** or build with appropriate drivers.

---

## 🔧 Troubleshooting

* **Wrong local time**
  Set timezone (`Timezone 3`), disable DST if needed (`SetOption36 0`), then `SetTime`.

* **JSON parsing errors**
  Tasmota sometimes returns numbers as strings or floats (e.g., `WifiPower: 16.0`).
  Models should use `JsonNumberHandling.AllowReadingFromString` and/or `double?` for such fields.

* **Wi-Fi changes don’t take effect**
  After `SSID1`/`Password1`, device may need `Restart`. Use `WifiConfig 4` so it falls back to AP mode if it can’t join.

* **Rules didn’t fire**
  Ensure rule is **enabled** (`RuleN 1`). For one-shot, helper rule (Status7 poke) is added automatically.

---

## 🗺️ Roadmap Ideas

* Optional MQTT client for publish/subscribe instead of HTTP polling
* Higher-level cron-like scheduler on top of Timers
* Rule editor/parser improvements (day masks within rules)

---

## 📄 License

Use freely in your projects. Attribution appreciated. 💚

---

## 🙌 Acknowledgements

* [Tasmota](https://tasmota.github.io/) project and community.
