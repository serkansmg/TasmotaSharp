// TasmotaClient.cs
// Usage quick start:
//   var client = new TasmotaSharp.TasmotaClient("10.0.4.41");
//
//   // Relay control
//   await client.SetRelayAsync(1, true);              // Relay1 ON
//   var isOn = await client.GetRelayStateAsync(1);    // state
//
//   // Time & timezone
//   await client.SetTimezoneAsync(3);                  // UTC+3
//   await client.SetDstAsync(false);                   // DST off
//   await client.SetTimeAsync(DateTime.Now);          // set local time
//
//   // DS3231 sensor read (requires sensors build / All Sensors)
//   var sns = await client.GetSensorStatusAsync();     // Status 10
//
//   // Timers (weekly schedule)
//   await client.SetTimerAsync(1, new TimeSpan(18,30,0), new[]{DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Saturday}, output:1, action:1); // 18:30 ON
//   await client.SetTimerAsync(2, new TimeSpan(23, 0,0), new[]{DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Saturday}, output:1, action:0); // 23:00 OFF
//
//   // One-shot at specific date-time: 2025-09-05 18:30 -> ON then 30s later OFF
//   await client.SetOneShotDateRuleAsync(1, new DateTime(2025,9,5,18,30,0), output:1, onWhenTrue:true, pulse:TimeSpan.FromSeconds(30));
//
//   // “Now + 10s” relative pulse: 10s sonra ON, +10s sonra OFF
//   await client.ScheduleOneShotInAsync(TimeSpan.FromSeconds(10), output:1, onWhenTrue:true, pulse:TimeSpan.FromSeconds(10));
//
//   // Rule get/delete
//   var r1 = await client.GetRuleInfoAsync(1);
//   await client.DeleteRuleAsync(1);
//
//   // Wi-Fi scan
//   var nets = await client.ScanWifiAsync();
//
//   // Config backup/restore
//   var bytes = await client.BackupConfigAsync();       // save .dmp
//   await client.RestoreConfigAsync(bytes);             // restore

using System.Text.Json;
using System.Text.Json.Serialization;
using TasmotaSharp.Models;

namespace TasmotaSharp;

/// <summary>
/// Lightweight HTTP client for Tasmota (ESP32) HTTP API ("/cm?cmnd=").
/// Includes helpers for relays, time/zone, Wi-Fi, MQTT, timers, rules, and simple rule builder.
/// </summary>
public class TasmotaClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _rootUrl;

    /// <param name="ipAddress">Device IP or host (e.g. "10.0.4.41" or "tasmota-xxxx.local")</param>
    public TasmotaClient(string ipAddress)
    {
        _http = new HttpClient();
        _baseUrl = $"http://{ipAddress}/cm?cmnd=";
        _rootUrl = $"http://{ipAddress}";
    }

    #region Core HTTP helpers

    private async Task<JsonDocument?> SendCommandAsync(string command)
    {
        try
        {
            var url = _baseUrl + Uri.EscapeDataString(command);
            var response = await _http.GetStringAsync(url);
            return JsonDocument.Parse(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SendCommandAsync] Hata: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> SendCommandGetStringAsync(string command)
    {
        try
        {
            var url = _baseUrl + Uri.EscapeDataString(command);
            return await _http.GetStringAsync(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SendCommandGetStringAsync] Hata: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Relay controls

    /// <summary>Set relay n to ON/OFF (n = 1..8)</summary>
    public async Task<bool?> SetRelayAsync(int relay, bool state)
    {
        try
        {
            var doc = await SendCommandAsync($"Power{relay} {(state ? "ON" : "OFF")}");
            if (doc == null) return null;
            return doc.RootElement.EnumerateObject()
                .First(p => p.Name.StartsWith($"POWER{relay}", StringComparison.OrdinalIgnoreCase))
                .Value.GetString() == "ON";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SetRelayAsync] Hata: {ex.Message}");
            return null;
        }
    }

    /// <summary>Toggle relay n (n = 1..8)</summary>
    public async Task<bool?> ToggleRelayAsync(int relay)
    {
        try
        {
            var doc = await SendCommandAsync($"Power{relay} TOGGLE");
            if (doc == null) return null;
            return doc.RootElement.EnumerateObject()
                .First(p => p.Name.StartsWith($"POWER{relay}", StringComparison.OrdinalIgnoreCase))
                .Value.GetString() == "ON";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ToggleRelayAsync] Hata: {ex.Message}");
            return null;
        }
    }

    /// <summary>Get relay n state (n = 1..8)</summary>
    public async Task<bool?> GetRelayStateAsync(int relay)
    {
        try
        {
            using var doc = await SendCommandAsync($"Power{relay}");
            if (doc == null) return null;
            return doc.RootElement.EnumerateObject()
                .First(p => p.Name.StartsWith($"POWER{relay}", StringComparison.OrdinalIgnoreCase))
                .Value.GetString() == "ON";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetRelayStateAsync] Hata: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Time / timezone / DST

    /// <summary>Read device time (Tasmota "Time")</summary>
    public async Task<DateTime?> GetTimeAsync()
    {
        try
        {
            using var doc = await SendCommandAsync("Time");
            if (doc == null) return null;
            var data = doc.RootElement.GetProperty("Time").GetString();
            return DateTime.Parse(data!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetTimeAsync] Hata: {ex.Message}");
            return null;
        }
    }

    /// <summary>Set device time (also writes to RTC when present)</summary>
    public async Task<bool> SetTimeAsync(DateTime dateTime)
    {
        try
        {
            await SendCommandAsync($"Time {dateTime:yyyy-MM-dd HH:mm:ss}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SetTimeAsync] Hata: {ex.Message}");
            return false;
        }
    }

    /// <summary>Set timezone offset (e.g. TR = 3)</summary>
    public async Task<bool> SetTimezoneAsync(int offset)
    {
        try
        {
            await SendCommandAsync($"Timezone {offset}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SetTimezoneAsync] Hata: {ex.Message}");
            return false;
        }
    }

    /// <summary>Enable/disable DST (SetOption36)</summary>
    public async Task<bool> SetDstAsync(bool enabled)
    {
        try
        {
            await SendCommandAsync($"SetOption36 {(enabled ? 1 : 0)}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SetDstAsync] Hata: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Status

    /// <summary>Full status (Status 0) mapped to TasmotaStatus model (loose types)</summary>
    public async Task<TasmotaStatus?> GetStatusAsync()
    {
        try
        {
            var doc = await SendCommandGetStringAsync("Status 0");
            if (doc == null) return null;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
            return JsonSerializer.Deserialize<TasmotaStatus>(doc, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetStatusAsync] Hata: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region mDNS

    /// <summary>Enable/disable mDNS (SetOption55)</summary>
    public async Task<bool> EnableMdnsAsync(bool enable)
    {
        try
        {
            await SendCommandAsync($"SetOption55 {(enable ? 1 : 0)}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EnableMdnsAsync] Hata: {ex.Message}");
            return false;
        }
    }

    /// <summary>Get mDNS state; true=ON, false=OFF, null=unknown</summary>
    public async Task<bool?> GetMdnsStateAsync()
    {
        try
        {
            var resp = await SendCommandGetStringAsync("SetOption55");
            if (string.IsNullOrWhiteSpace(resp)) return null;
            using var doc = JsonDocument.Parse(resp);
            if (doc.RootElement.TryGetProperty("SetOption55", out var v))
            {
                var s = v.GetString();
                if (string.Equals(s, "ON", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(s, "OFF", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetMdnsStateAsync] Hata: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Restart / Factory reset

    /// <summary>Soft restart</summary>
    public async Task<bool> RestartAsync()
    {
        try
        {
            await SendCommandAsync("Restart 1");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RestartAsync] Hata: {ex.Message}");
            return false;
        }
    }

    /// <summary>Factory reset; mode: 1,2,5 (1 basic, 2 keep Wi-Fi/MQTT, 5 full safest)</summary>
    public async Task<bool> FactoryResetAsync(int mode)
    {
        try
        {
            if (mode is not (1 or 2 or 5))
                throw new ArgumentOutOfRangeException(nameof(mode), "Reset mode must be 1, 2, or 5.");

            await SendCommandAsync($"Reset {mode}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FactoryResetAsync] Hata: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Config backup / restore

    /// <summary>Download config dump (/dl)</summary>
    public async Task<byte[]?> BackupConfigAsync()
    {
        try
        {
            var url = $"{_rootUrl}/dl";
            return await _http.GetByteArrayAsync(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BackupConfigAsync] Hata: {ex.Message}");
            return null;
        }
    }

    /// <summary>Upload config dump (/u)</summary>
    public async Task<bool> RestoreConfigAsync(byte[] dmpData)
    {
        try
        {
            var url = $"{_rootUrl}/u";
            using var content = new MultipartFormDataContent
            {
                { new ByteArrayContent(dmpData), "file", "config.dmp" }
            };
            var response = await _http.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RestoreConfigAsync] Hata: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region MQTT

    /// <summary>Set MQTT params; leaves nulls unchanged, optionally reconnects</summary>
    public async Task<bool> SetMqttAsync(
        string host,
        int? port = null,
        string? user = null,
        string? password = null,
        string? clientId = null,
        string? topic = null,
        string? fullTopic = null,
        bool reconnect = true)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(host))     await SendCommandAsync($"MqttHost {host}");
            if (port.HasValue)                        await SendCommandAsync($"MqttPort {port.Value}");
            if (!string.IsNullOrWhiteSpace(user))     await SendCommandAsync($"MqttUser {user}");
            if (!string.IsNullOrWhiteSpace(password)) await SendCommandAsync($"MqttPassword {password}");
            if (!string.IsNullOrWhiteSpace(clientId)) await SendCommandAsync($"MqttClient {clientId}");
            if (!string.IsNullOrWhiteSpace(topic))    await SendCommandAsync($"Topic {topic}");
            if (!string.IsNullOrWhiteSpace(fullTopic))await SendCommandAsync($"FullTopic {fullTopic}");
            if (reconnect)                            await SendCommandAsync("MqttReconnect 1");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SetMqttAsync] Hata: {ex.Message}");
            return false;
        }
    }

    /// <summary>Read StatusMQT block from Status 0</summary>
    public async Task<StatusMQT?> GetMqttStatusAsync()
    {
        try
        {
            var s = await GetStatusAsync();
            return s?.StatusMQT;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetMqttStatusAsync] Hata: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Wi-Fi

    /// <summary>Set SSID/PW (optionally restart)</summary>
    public async Task<bool> SetWifiCredentialsAsync(string ssid, string password, bool restartAfter = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ssid))
                throw new ArgumentException("SSID boş olamaz.", nameof(ssid));

            await SendCommandAsync($"SSID1 {ssid}");
            await SendCommandAsync($"Password1 {password}");
            await SendCommandAsync("WifiConfig 4"); // fail -> AP+Config

            if (restartAfter)
                await RestartAsync();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SetWifiCredentialsAsync] Hata: {ex.Message}");
            return false;
        }
    }

    /// <summary>Get Wi-Fi info (StatusNET + StatusSTS.Wifi)</summary>
    public async Task<(StatusNET? Net, Wifi? Wifi)?> GetWifiInfoAsync()
    {
        try
        {
            var st = await GetStatusAsync();
            if (st == null) return null;
            return (st.StatusNET, st.StatusSTS?.Wifi);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetWifiInfoAsync] Hata: {ex.Message}");
            return null;
        }
    }

    /// <summary>Wi-Fi scan (WifiScan 1 + poll WifiScan)</summary>
    public async Task<List<WifiScanResult>?> ScanWifiAsync(int timeoutMs = 5000, int pollIntervalMs = 400)
    {
        try
        {
            _ = await SendCommandGetStringAsync("WifiScan 1");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                await Task.Delay(pollIntervalMs);
                var raw = await SendCommandGetStringAsync("WifiScan");
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                using var doc = JsonDocument.Parse(raw);

                // still scanning?
                if (doc.RootElement.TryGetProperty("WifiScan", out var scanState) &&
                    scanState.ValueKind == JsonValueKind.String)
                {
                    var s = scanState.GetString();
                    if (string.Equals(s, "Scanning", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (string.Equals(s, "Not Started", StringComparison.OrdinalIgnoreCase))
                        return null;
                }

                if (!doc.RootElement.TryGetProperty("WiFiScan", out var resultsObj) ||
                    resultsObj.ValueKind != JsonValueKind.Object)
                    continue;

                var results = new List<WifiScanResult>();

                foreach (var apProp in resultsObj.EnumerateObject())
                {
                    var ap = apProp.Value;

                    string? ssid = null;
                    if (ap.TryGetProperty("SSID", out var pSSID)) ssid = pSSID.GetString();
                    else if (ap.TryGetProperty("SSId", out var pSSId)) ssid = pSSId.GetString();

                    string? bssid = ap.TryGetProperty("BSSId", out var pB) ? pB.GetString() : null;

                    int? channel = null;
                    if (ap.TryGetProperty("Channel", out var pCh))
                    {
                        if (pCh.ValueKind == JsonValueKind.Number && pCh.TryGetInt32(out var ch)) channel = ch;
                        else if (pCh.ValueKind == JsonValueKind.String && int.TryParse(pCh.GetString(), out var chs))
                            channel = chs;
                    }

                    int? signalDbm = null;
                    if (ap.TryGetProperty("Signal", out var pSig))
                    {
                        if (pSig.ValueKind == JsonValueKind.Number && pSig.TryGetInt32(out var sv)) signalDbm = sv;
                        else if (pSig.ValueKind == JsonValueKind.String && int.TryParse(pSig.GetString(), out var svs))
                            signalDbm = svs;
                    }

                    int? rssiPercent = null;
                    if (ap.TryGetProperty("RSSI", out var pRssi))
                    {
                        if (pRssi.ValueKind == JsonValueKind.Number && pRssi.TryGetInt32(out var rv)) rssiPercent = rv;
                        else if (pRssi.ValueKind == JsonValueKind.String && int.TryParse(pRssi.GetString(), out var rvs)) rssiPercent = rvs;
                    }

                    string? enc = ap.TryGetProperty("Encryption", out var pEnc) ? pEnc.GetString() : null;

                    results.Add(new WifiScanResult
                    {
                        SSID = ssid,
                        BSSId = bssid,
                        Channel = channel,
                        SignalDbm = signalDbm,
                        RssiPercent = rssiPercent,
                        Encryption = enc
                    });
                }

                return results;
            }

            return null; // timeout
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ScanWifiAsync] Hata: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region LED helpers

    public async Task<bool> SetLedStateAsync(int mode /*0..8*/)
    {
        try
        {
            await SendCommandAsync($"LedState {mode}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SetLedStateAsync] Hata: {ex.Message}");
            return false;
        }
    }

    /// <summary>Set LedPowerN ON/OFF (if present)</summary>
    public async Task<bool?> SetLedPowerAsync(int index, bool on)
    {
        try
        {
            var doc = await SendCommandAsync($"LedPower{index} {(on ? "ON" : "OFF")}");
            if (doc == null) return null;
            var prop = doc.RootElement.EnumerateObject()
                .FirstOrDefault(p => p.Name.StartsWith($"LedPower{index}", StringComparison.OrdinalIgnoreCase));
            return prop.Value.GetString()?.Equals("ON", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SetLedPowerAsync] Hata: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Sensors / telemetry

    /// <summary>Status 10 mapped to TasmotaSensorStatus (e.g., DS3231)</summary>
    public async Task<TasmotaSensorStatus?> GetSensorStatusAsync()
    {
        try
        {
            var json = await SendCommandGetStringAsync("Status 10");
            if (json is null) return null;
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
            return JsonSerializer.Deserialize<TasmotaSensorStatus>(json, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetSensorStatusAsync] Hata: {ex.Message}");
            return null;
        }
    }

    /// <summary>Set telemetry period (seconds)</summary>
    public async Task<bool> SetTelePeriodAsync(int seconds)
    {
        try { await SendCommandAsync($"TelePeriod {seconds}"); return true; }
        catch (Exception ex) { Console.WriteLine($"[SetTelePeriodAsync] Hata: {ex.Message}"); return false; }
    }

    #endregion

    #region Timers (weekly schedule)

    // Tasmota "Days" field is 7 chars for Sun..Sat (S M T W T F S). '-'=off, any char=on.
    private static string BuildDaysMask(params DayOfWeek[] days)
    {
        var mask = new char[] { '-', '-', '-', '-', '-', '-', '-' };
        foreach (var d in days)
        {
            int idx = d switch
            {
                DayOfWeek.Sunday => 0,
                DayOfWeek.Monday => 1,
                DayOfWeek.Tuesday => 2,
                DayOfWeek.Wednesday => 3,
                DayOfWeek.Thursday => 4,
                DayOfWeek.Friday => 5,
                DayOfWeek.Saturday => 6,
                _ => 0
            };
            mask[idx] = '1';
        }
        return new string(mask);
    }

    /// <summary>
    /// Create/update TimerN with friendly params.
    /// action: 0=OFF,1=ON,2=TOGGLE,3=Rule; output: relay index (1..8); index: 1..16
    /// </summary>
    public async Task<bool> SetTimerAsync(
        int index,
        TimeSpan time,
        IEnumerable<DayOfWeek> days,
        int output,
        int action,
        bool repeat = true,
        int mode = 0,
        int window = 0)
    {
        try
        {
            if (index < 1 || index > 16) throw new ArgumentOutOfRangeException(nameof(index));
            var daysMask = BuildDaysMask(days?.ToArray() ?? Array.Empty<DayOfWeek>());
            var payload = $"{{\"Enable\":1,\"Time\":\"{time:hh\\:mm}\",\"Window\":{window},\"Days\":\"{daysMask}\",\"Repeat\":{(repeat ? 1 : 0)},\"Output\":{output},\"Action\":{action},\"Mode\":{mode}}}";
            // NOTE: time uses 24h? use HH:mm to be explicit:
            payload = payload.Replace($"{time:hh\\:mm}", time.ToString("HH\\:mm"));
            await SendCommandAsync($"Timer{index} {payload}");
            await SendCommandAsync("Timers 1"); // ensure timers subsystem is on
            return true;
        }
        catch (Exception ex) { Console.WriteLine($"[SetTimerAsync] Hata: {ex.Message}"); return false; }
    }

    /// <summary>Same as SetTimerAsync but accepts raw day mask string e.g. "1-1-1--"</summary>
    public async Task<bool> SetTimerByMaskAsync(
        int index,
        string daysMask,
        string timeHHmm,
        int output,
        int action,
        bool repeat = true,
        int mode = 0,
        int window = 0)
    {
        try
        {
            var payload = $"{{\"Enable\":1,\"Time\":\"{timeHHmm}\",\"Window\":{window},\"Days\":\"{daysMask}\",\"Repeat\":{(repeat ? 1 : 0)},\"Output\":{output},\"Action\":{action},\"Mode\":{mode}}}";
            await SendCommandAsync($"Timer{index} {payload}");
            await SendCommandAsync("Timers 1");
            return true;
        }
        catch (Exception ex) { Console.WriteLine($"[SetTimerByMaskAsync] Hata: {ex.Message}"); return false; }
    }

    public async Task<bool> EnableAllTimersAsync(bool enable)
    {
        try { await SendCommandAsync($"Timers {(enable ? 1 : 0)}"); return true; }
        catch (Exception ex) { Console.WriteLine($"[EnableAllTimersAsync] Hata: {ex.Message}"); return false; }
    }

    public async Task<bool> DisableTimerAsync(int index)
    {
        try { await SendCommandAsync($"Timer{index} 0"); return true; }
        catch (Exception ex) { Console.WriteLine($"[DisableTimerAsync] Hata: {ex.Message}"); return false; }
    }

    /// <summary>Disable and clear TimerN payload</summary>
    public async Task<bool> ClearTimerAsync(int index)
    {
        try
        {
            await SendCommandAsync($"Timer{index} 0");
            await SendCommandAsync($"Timer{index} {{}}");
            return true;
        }
        catch (Exception ex) { Console.WriteLine($"[ClearTimerAsync] Hata: {ex.Message}"); return false; }
    }

    #endregion

    #region Geo (sunrise/sunset)

    public async Task<bool> SetGeoAsync(double latitude, double longitude)
    {
        try
        {
            await SendCommandAsync($"Latitude {latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            await SendCommandAsync($"Longitude {longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            return true;
        }
        catch (Exception ex) { Console.WriteLine($"[SetGeoAsync] Hata: {ex.Message}"); return false; }
    }

    #endregion

    #region Rules (one-shot, relative pulse, sunrise/sunset) + management

    /// <summary>
    /// One-shot absolute date/time rule:
    /// at 'whenLocal' do Power{output} on/off, optional pulse then revert.
    /// Internally uses StatusTIM#Local pattern + Time#Minute+Status 7 helper.
    /// </summary>
    public async Task<bool> SetOneShotDateRuleAsync(
        int ruleIndex,
        DateTime whenLocal,
        int output,
        bool onWhenTrue,
        TimeSpan? pulse = null)
    {
        try
        {
            if (ruleIndex is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(ruleIndex));

            string pattern = whenLocal.ToString("-MM-dd'T'HH:mm"); // e.g. -09-05T18:30
            string actionCmd = onWhenTrue ? $"Power{output} 1" : $"Power{output} 0";

            string backlog;
            if (pulse.HasValue)
            {
                int delayDs = (int)Math.Round(pulse.Value.TotalSeconds) * 10; // Delay uses deciseconds!
                backlog = $"Backlog {actionCmd}; Delay {delayDs}; Power{output} {(onWhenTrue ? 0 : 1)}";
            }
            else
            {
                backlog = actionCmd;
            }

            string ruleCmd = $"Rule{ruleIndex} on StatusTIM#Local$|{pattern} do {backlog} endon";
            await SendCommandAsync(ruleCmd);

            int minuteOfDay = whenLocal.Hour * 60 + whenLocal.Minute;
            int aux = ruleIndex == 3 ? 1 : ruleIndex + 1;
            string auxRuleCmd = $"Rule{aux} on Time#Minute={minuteOfDay} do Status 7 endon";

            await SendCommandAsync(auxRuleCmd);
            await SendCommandAsync($"Rule{ruleIndex} 1");
            await SendCommandAsync($"Rule{aux} 1");
            return true;
        }
        catch (Exception ex) { Console.WriteLine($"[SetOneShotDateRuleAsync] Hata: {ex.Message}"); return false; }
    }

    public async Task<bool> EnableRuleAsync(int ruleIndex, bool enable)
    {
        try { await SendCommandAsync($"Rule{ruleIndex} {(enable ? 1 : 0)}"); return true; }
        catch (Exception ex) { Console.WriteLine($"[EnableRuleAsync] Hata: {ex.Message}"); return false; }
    }

    public async Task<bool> SetRuleScriptAsync(int ruleIndex, string script)
    {
        try { await SendCommandAsync($"Rule{ruleIndex} {script}"); return true; }
        catch (Exception ex) { Console.WriteLine($"[SetRuleScriptAsync] Hata: {ex.Message}"); return false; }
    }

    /// <summary>Disable and clear rule script</summary>
    public async Task<bool> ClearRuleAsync(int ruleIndex)
    {
        try
        {
            await SendCommandAsync($"Rule{ruleIndex} 0");
            await SendCommandAsync($"Rule{ruleIndex} \"\"");
            return true;
        }
        catch (Exception ex) { Console.WriteLine($"[ClearRuleAsync] Hata: {ex.Message}"); return false; }
    }

    /// <summary>Start RuleTimerN countdown in seconds (for relative pulse)</summary>
    public async Task<bool> StartRuleTimerAsync(int index, int seconds)
    {
        try { await SendCommandAsync($"RuleTimer{index} {seconds}"); return true; }
        catch (Exception ex) { Console.WriteLine($"[StartRuleTimerAsync] Hata: {ex.Message}"); return false; }
    }

    /// <summary>
    /// Schedule "now + delay" one-shot (uses absolute helper internally).
    /// Example: await ScheduleOneShotInAsync(TimeSpan.FromSeconds(10), 1, true, TimeSpan.FromSeconds(10));
    /// </summary>
    public async Task<bool> ScheduleOneShotInAsync(
        TimeSpan delay,
        int output,
        bool onWhenTrue,
        TimeSpan? pulse = null,
        int ruleIndex = 1)
    {
        try
        {
            var whenLocal = DateTime.Now.Add(delay);
            return await SetOneShotDateRuleAsync(ruleIndex, whenLocal, output, onWhenTrue, pulse);
        }
        catch (Exception ex) { Console.WriteLine($"[ScheduleOneShotInAsync] Hata: {ex.Message}"); return false; }
    }

    private static bool OnOffToBool(string? s) => string.Equals(s, "ON", StringComparison.OrdinalIgnoreCase);

    /// <summary>Read RuleN state/script; supports both nested and flat JSON variants</summary>
    public async Task<RuleInfo?> GetRuleInfoAsync(int ruleIndex)
    {
        try
        {
            var raw = await SendCommandGetStringAsync($"Rule{ruleIndex}");
            if (string.IsNullOrWhiteSpace(raw)) return null;

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (!root.TryGetProperty($"Rule{ruleIndex}", out var ruleProp))
                return null;

            bool enabled = false, once = false, stopOnError = false;
            int? free = null, length = null;
            string? script = null;

            if (ruleProp.ValueKind == JsonValueKind.Object)
            {
                enabled = ruleProp.TryGetProperty("State", out var st) && OnOffToBool(st.GetString());
                once = ruleProp.TryGetProperty("Once", out var o) && OnOffToBool(o.GetString());
                stopOnError = ruleProp.TryGetProperty("StopOnError", out var soe) && OnOffToBool(soe.GetString());
                if (ruleProp.TryGetProperty("Free", out var f) && f.ValueKind == JsonValueKind.Number) free = f.GetInt32();
                if (ruleProp.TryGetProperty("Length", out var ln) && ln.ValueKind == JsonValueKind.Number) length = ln.GetInt32();
                if (ruleProp.TryGetProperty("Rules", out var r)) script = r.GetString();
            }
            else
            {
                enabled = OnOffToBool(ruleProp.GetString());
                once = root.TryGetProperty("Once", out var o) && OnOffToBool(o.GetString());
                stopOnError = root.TryGetProperty("StopOnError", out var soe) && OnOffToBool(soe.GetString());
                if (root.TryGetProperty("Free", out var f) && f.ValueKind == JsonValueKind.Number) free = f.GetInt32();
                if (root.TryGetProperty("Length", out var ln) && ln.ValueKind == JsonValueKind.Number) length = ln.GetInt32();
                if (root.TryGetProperty("Rules", out var r)) script = r.GetString();
            }

            return new RuleInfo
            {
                Index = ruleIndex,
                Enabled = enabled,
                Once = once,
                StopOnError = stopOnError,
                Free = free,
                Length = length,
                Script = script
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetRuleInfoAsync] Hata: {ex.Message}");
            return null;
        }
    }

    public async Task<List<RuleInfo>> GetAllRulesAsync()
    {
        var list = new List<RuleInfo>();
        for (int i = 1; i <= 3; i++)
        {
            var ri = await GetRuleInfoAsync(i);
            if (ri != null) list.Add(ri);
        }
        return list;
    }

    /// <summary>Disable + clear script (alias of ClearRuleAsync)</summary>
    public async Task<bool> DeleteRuleAsync(int ruleIndex) => await ClearRuleAsync(ruleIndex);

    /// <summary>
    /// Relative pulse in one call: after startDelaySeconds -> ON, after pulseSeconds -> OFF.
    /// Uses RuleTimer + Rules#Timer event.
    /// </summary>
    public async Task<bool> PulseAfterAsync(int startDelaySeconds, int output, int pulseSeconds, int ruleIndex = 1, int timerIndex = 1)
    {
        try
        {
            await SetRuleScriptAsync(ruleIndex, $"on Rules#Timer={timerIndex} do backlog Power{output} 1; Delay {pulseSeconds * 10}; Power{output} 0 endon");
            await EnableRuleAsync(ruleIndex, true);
            await StartRuleTimerAsync(timerIndex, startDelaySeconds);
            return true;
        }
        catch (Exception ex) { Console.WriteLine($"[PulseAfterAsync] Hata: {ex.Message}"); return false; }
    }

    #endregion

    #region Simple rule model + builder (optional high-level API)

  
    private static string BuildBacklogPulse(int output, bool onWhenTrue, int? pulseSeconds, bool autoDisable, int ruleIndex)
    {
        string onCmd = $"Power{output} {(onWhenTrue ? 1 : 0)}";
        string offCmd = $"Power{output} {(onWhenTrue ? 0 : 1)}";

        if (pulseSeconds is > 0)
        {
            var delay = pulseSeconds.Value * 10; // deciseconds
            var tail = autoDisable ? $"; Rule{ruleIndex} 0" : "";
            return $"Backlog {onCmd}; Delay {delay}; {offCmd}{tail}";
        }
        else
        {
            var tail = autoDisable ? $"; Rule{ruleIndex} 0" : "";
            return $"Backlog {onCmd}{tail}";
        }
    }

    private static (string rule1, string? rule2) BuildRuleScripts(SimpleRule s, int ruleIndex, int timerIndex = 1)
    {
        switch (s.Type)
        {
            case SimpleRuleType.OneShotAtLocalTime:
            {
                if (s.WhenLocal == null) throw new ArgumentException("WhenLocal null olamaz.");
                var when = s.WhenLocal.Value;
                string pattern = when.ToString("-MM-dd'T'HH':'mm"); // "-09-05T18:30"
                int minuteOfDay = when.Hour * 60 + when.Minute;

                var action = BuildBacklogPulse(s.Output, s.OnWhenTrue, (int?)s.Pulse?.TotalSeconds, s.AutoDisable, ruleIndex);

                string ruleA = $"on StatusTIM#Local$|{pattern} do {action} endon";
                string ruleB = $"on Time#Minute={minuteOfDay} do Status 7 endon";
                return (ruleA, ruleB);
            }

            case SimpleRuleType.RelativePulse:
            {
                if (s.StartDelaySeconds is null || s.PulseSeconds is null)
                    throw new ArgumentException("StartDelaySeconds ve PulseSeconds gerekli.");
                var action = BuildBacklogPulse(s.Output, s.OnWhenTrue, s.PulseSeconds, s.AutoDisable, ruleIndex);
                string ruleA = $"on Rules#Timer={timerIndex} do {action} endon";
                return (ruleA, null);
            }

            case SimpleRuleType.SunriseSunset:
            {
                if (s.UseSunset is null) throw new ArgumentException("UseSunset gerekli.");
                var baseEvent = s.UseSunset.Value ? "Sunset" : "Sunrise";
                var action = BuildBacklogPulse(s.Output, s.OnWhenTrue, (int?)s.Pulse?.TotalSeconds, s.AutoDisable, ruleIndex);

                if (s.OffsetMinutes is int off && off != 0)
                {
                    int delay = Math.Abs(off) * 60 * 10;
                    string delayPart = $"Delay {delay}; ";
                    string ruleA = $"on {baseEvent} do Backlog {delayPart}{action} endon";
                    return (ruleA, null);
                }
                else
                {
                    string ruleA = $"on {baseEvent} do {action} endon";
                    return (ruleA, null);
                }
            }

            default:
                throw new NotSupportedException("Unknown/unsupported SimpleRuleType");
        }
    }

    /// <summary>Compile and apply SimpleRule; writes RuleN (and helper) and enables them.</summary>
    public async Task<bool> ApplySimpleRuleAsync(int ruleIndex, SimpleRule rule, int timerIndexForRelative = 1)
    {
        try
        {
            var (r1, r2) = BuildRuleScripts(rule, ruleIndex, timerIndexForRelative);

            await SetRuleScriptAsync(ruleIndex, r1);
            await EnableRuleAsync(ruleIndex, true);

            if (!string.IsNullOrEmpty(r2))
            {
                int aux = ruleIndex == 3 ? 1 : ruleIndex + 1;
                await SetRuleScriptAsync(aux, r2);
                await EnableRuleAsync(aux, true);
            }

            if (rule.Type == SimpleRuleType.RelativePulse && rule.StartDelaySeconds is int sec)
            {
                await StartRuleTimerAsync(timerIndexForRelative, sec);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ApplySimpleRuleAsync] Hata: {ex.Message}");
            return false;
        }
    }

    /// <summary>Parse common patterns into SimpleRule (best-effort)</summary>
    public async Task<SimpleRule?> GetSimpleRuleAsync(int ruleIndex)
    {
        var info = await GetRuleInfoAsync(ruleIndex);
        if (info?.Script == null) return null;

        var script = info.Script;

        // OneShot pattern: on StatusTIM#Local$|-MM-ddTHH:mm do ...
        if (script.IndexOf("StatusTIM#Local$|-", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var m = System.Text.RegularExpressions.Regex.Match(script, @"-([0-1]\d)-([0-3]\d)T([0-2]\d):([0-5]\d)");
            if (m.Success)
            {
                var now = DateTime.Now;
                var target = new DateTime(now.Year, int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value),
                                          int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value), 0);
                var mPower = System.Text.RegularExpressions.Regex.Match(script, @"Power(\d)\s+([01])");
                int output = mPower.Success ? int.Parse(mPower.Groups[1].Value) : 1;
                bool onTrue = mPower.Success ? (mPower.Groups[2].Value == "1") : true;

                var mDelay = System.Text.RegularExpressions.Regex.Match(script, @"Delay\s+(\d+)");
                int? pulseSec = mDelay.Success ? int.Parse(mDelay.Groups[1].Value) / 10 : (int?)null;

                return new SimpleRule
                {
                    Type = SimpleRuleType.OneShotAtLocalTime,
                    Output = output,
                    OnWhenTrue = onTrue,
                    WhenLocal = target,
                    Pulse = pulseSec.HasValue ? TimeSpan.FromSeconds(pulseSec.Value) : null,
                    AutoDisable = script.Contains($"Rule{ruleIndex} 0", StringComparison.OrdinalIgnoreCase)
                };
            }
        }

        // RelativePulse pattern: on Rules#Timer=N do ...
        if (script.IndexOf("Rules#Timer=", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var mPower = System.Text.RegularExpressions.Regex.Match(script, @"Power(\d)\s+([01])");
            int output = mPower.Success ? int.Parse(mPower.Groups[1].Value) : 1;
            bool onTrue = mPower.Success ? (mPower.Groups[2].Value == "1") : true;
            var mDelay = System.Text.RegularExpressions.Regex.Match(script, @"Delay\s+(\d+)");
            int? pulseSec = mDelay.Success ? int.Parse(mDelay.Groups[1].Value) / 10 : (int?)null;

            return new SimpleRule
            {
                Type = SimpleRuleType.RelativePulse,
                Output = output,
                OnWhenTrue = onTrue,
                PulseSeconds = pulseSec,
                // StartDelaySeconds is unknown (Tasmota doesn't expose last RuleTimer value)
            };
        }

        // Sunrise/Sunset pattern
        if (script.IndexOf("on Sunrise", StringComparison.OrdinalIgnoreCase) >= 0 ||
            script.IndexOf("on Sunset", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            bool useSunset = script.IndexOf("Sunset", StringComparison.OrdinalIgnoreCase) >= 0;
            var mPower = System.Text.RegularExpressions.Regex.Match(script, @"Power(\d)\s+([01])");
            int output = mPower.Success ? int.Parse(mPower.Groups[1].Value) : 1;
            bool onTrue = mPower.Success ? (mPower.Groups[2].Value == "1") : true;
            var mDelay = System.Text.RegularExpressions.Regex.Match(script, @"Delay\s+(\d+)");
            int? pulseSec = mDelay.Success ? int.Parse(mDelay.Groups[1].Value) / 10 : (int?)null;

            return new SimpleRule
            {
                Type = SimpleRuleType.SunriseSunset,
                Output = output,
                OnWhenTrue = onTrue,
                UseSunset = useSunset,
                Pulse = pulseSec.HasValue ? TimeSpan.FromSeconds(pulseSec.Value) : null,
                AutoDisable = script.Contains($"Rule{ruleIndex} 0", StringComparison.OrdinalIgnoreCase)
            };
        }

        return new SimpleRule { Type = SimpleRuleType.Unknown };
    }

    #endregion
    
 



/// <summary>
/// Aynı günlerde, aynı saatlerde BİRDEN FAZLA röleyi AÇ/KAPAT programlar.
/// Örn: Pzt-Sal-Çar 09:00 AÇ, 22:00 KAPAT -> outputs={1,2,3}
/// </summary>
/// <param name="days">Gün listesi (örn new[]{Monday,Tuesday,Wednesday})</param>
/// <param name="onTimeHHmm">"HH:mm" 24s (örn "09:00")</param>
/// <param name="offTimeHHmm">"HH:mm" 24s (örn "22:00")</param>
/// <param name="outputs">Röle listesi (1..8)</param>
/// <param name="strategy">Timers veya RuleBacklog</param>
/// <param name="startTimerIndex">
/// Timers modunda hangi timer slotundan başlayacağını belirtir (default 1).
/// Her röle için 2 timer kullanılır (ON/OFF), yani toplam 2 * outputs.Count() slot gerekir.
/// </param>
/// <param name="ruleIndex">
/// RuleBacklog modunda hangi Rule slotunun (1..3) kullanılacağı.
/// Aynı rule içine hem ON hem OFF blokları yazılır.
/// </param>
public async Task<bool> SetTimerMultiAsync(
    IEnumerable<DayOfWeek> days,
    string onTimeHHmm,
    string offTimeHHmm,
    IEnumerable<int> outputs,
    MultiScheduleStrategy strategy = MultiScheduleStrategy.Timers,
    int startTimerIndex = 1,
    int ruleIndex = 1)
{
    try
    {
        if (outputs is null) throw new ArgumentNullException(nameof(outputs));
        var outs = outputs.ToArray();
        if (outs.Length == 0) throw new ArgumentException("En az bir röle vermelisin.", nameof(outputs));
        if (outs.Any(o => o < 1 || o > 8)) throw new ArgumentOutOfRangeException(nameof(outputs), "Röle numaraları 1..8 olmalı.");

        // On/Off dakikaya çevir (RuleBacklog için)
        static int MinuteOfDay(string hhmm)
        {
            var t = TimeSpan.ParseExact(hhmm, "hh\\:mm", System.Globalization.CultureInfo.InvariantCulture);
            // 24 saat formatı kullanmak istersek "HH:mm" parse edelim:
            if (!TimeSpan.TryParseExact(hhmm, "hh\\:mm", null, out t))
                t = TimeSpan.ParseExact(hhmm, "HH\\:mm", System.Globalization.CultureInfo.InvariantCulture);
            return t.Hours * 60 + t.Minutes;
        }

        if (strategy == MultiScheduleStrategy.Timers)
        {
            // Her röle için 2 timer (ON/OFF) yazarız.
            // Toplam ihtiyaç = outs.Length * 2  (Timer1..Timer16)
            int need = outs.Length * 2;
            if (startTimerIndex < 1 || startTimerIndex + need - 1 > 16)
                throw new InvalidOperationException($"Timer slotları yetmiyor. startTimerIndex={startTimerIndex}, ihtiyaç={need}, sınır=16.");

            // Gün maskesini hazırla
            var daysArr = days?.ToArray() ?? Array.Empty<DayOfWeek>();

            int timer = startTimerIndex;
            foreach (var o in outs)
            {
                // ON
                bool ok1 = await SetTimerByMaskAsync(
                    index: timer++,
                    daysMask: BuildDaysMask(daysArr),
                    timeHHmm: onTimeHHmm,
                    output: o,
                    action: 1   // ON
                );
                if (!ok1) return false;

                // OFF
                bool ok2 = await SetTimerByMaskAsync(
                    index: timer++,
                    daysMask: BuildDaysMask(daysArr),
                    timeHHmm: offTimeHHmm,
                    output: o,
                    action: 0   // OFF
                );
                if (!ok2) return false;
            }

            // Timer sistemini açık tut
            await EnableAllTimersAsync(true);
            return true;
        }
        else // RuleBacklog
        {
            if (ruleIndex is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(ruleIndex), "Rule index 1..3 olmalı.");

            int onMinute  = MinuteOfDay(onTimeHHmm);
            int offMinute = MinuteOfDay(offTimeHHmm);

            // Aynı günleri sınırlamak istiyorsan Rule içinde gün kontrolü yapmak zahmetli;
            // Timer’lar günleri native olarak yönetir. RuleBacklog stratejisinde
            // genellikle her gün aynı dakikada tetiklenir. Gün bazlı kısıt
            // gerekiyorsa ilave Rule bloklarıyla maskeleme yapılmalı (ileri düzey).
            // Basitlik için direkt Time#Minute kullanıyoruz.

            // Backlog parçaları
            string onBacklog  = string.Join("; ", outs.Select(o => $"Power{o} ON"));
            string offBacklog = string.Join("; ", outs.Select(o => $"Power{o} OFF"));

            string ruleScript =
                $"on Time#Minute={onMinute} do Backlog {onBacklog} endon " +
                $"on Time#Minute={offMinute} do Backlog {offBacklog} endon";

            // Rule yaz ve enable et
            if (!await SetRuleScriptAsync(ruleIndex, ruleScript)) return false;
            return await EnableRuleAsync(ruleIndex, true);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SetTimerMultiAsync] Hata: {ex.Message}");
        return false;
    }
}

}
