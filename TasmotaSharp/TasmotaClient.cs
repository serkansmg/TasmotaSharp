// File: TasmotaClient.cs
// Usage quick start:
//   var client = new TasmotaSharp.TasmotaClient("10.0.4.41");
//
//   // Relay control
//   await client.SetRelayAsync(1, true);              // Relay1 ON
//   var isOn = await client.GetRelayStateAsync(1);    // state
//
//   // Time & timezone
//   await client.SetTimezoneAsync(3);                 // UTC+3
//   await client.SetDstAsync(false);                  // DST off
//   await client.SetTimeAsync(DateTime.Now);          // set local time
//
//   // DS3231 sensor read (requires sensors build / All Sensors)
//   var sns = await client.GetSensorStatusAsync();    // Status 10
//
//   // Timers (weekly schedule)
//   await client.SetTimerAsync(
//       1, new TimeSpan(18,30,0),
//       new[]{ DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Saturday },
//       output:1, action:1);                          // 18:30 ON
//   await client.SetTimerAsync(
//       2, new TimeSpan(23,0,0),
//       new[]{ DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Saturday },
//       output:1, action:0);                          // 23:00 OFF
//
//   // One-shot at specific date-time: 2025-09-05 18:30 -> ON then 30s later OFF
//   await client.SetOneShotDateRuleAsync(1,
//       new DateTime(2025,9,5,18,30,0),
//       output:1, onWhenTrue:true, pulse:TimeSpan.FromSeconds(30));
//
//   // "Now + 10s" relative pulse: ON after 10s, OFF after another 10s
//   await client.ScheduleOneShotInAsync(TimeSpan.FromSeconds(10),
//       output:1, onWhenTrue:true, pulse:TimeSpan.FromSeconds(10));
//
//   // Rule get/delete
//   var r1 = await client.GetRuleInfoAsync(1);
//   await client.DeleteRuleAsync(1);
//
//   // Wi-Fi scan
//   var nets = await client.ScanWifiAsync();
//
//   // Config backup/restore
//   var bytes = await client.BackupConfigAsync();     // save .dmp
//   await client.RestoreConfigAsync(bytes);           // restore

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TasmotaSharp.Models;

namespace TasmotaSharp;

/// <summary>
/// Lightweight HTTP client for Tasmota (ESP32/ESP8266) HTTP API ("/cm?cmnd=").
/// Includes helpers for relays, time/zone, Wi-Fi, MQTT, timers, rules, geo, telemetry, and a simple rule builder.
/// </summary>
public class TasmotaClient : IDisposable
{
    private readonly ILogger<TasmotaClient>? _logger;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly HttpClient? _fallbackHttp;
    private string _baseUrl;
    private string _rootUrl;

    /// <param name="ipAddress">Device IP or host (e.g. "10.0.4.41" or "tasmota-xxxx.local")</param>
    public TasmotaClient(string ipAddress,
                         ILogger<TasmotaClient>? logger = null,
                         IHttpClientFactory? httpClientFactory = null)
    {
        try
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;

            if (_httpClientFactory is null)
            {
                _fallbackHttp = new HttpClient(new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
                })
                { Timeout = TimeSpan.FromSeconds(3) }; // Reduced from 15 to 3 seconds
            }

            _rootUrl = NormalizeRoot(ipAddress);
            _baseUrl = $"{_rootUrl}/cm?cmnd=";
            
            LogInformation("TasmotaClient initialized for {Url}", _rootUrl);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to initialize TasmotaClient with IP {IpAddress}", ipAddress);
            throw;
        }
    }

    public TasmotaClient(
        ILogger<TasmotaClient>? logger = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        try
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;

            if (_httpClientFactory is null)
            {
                _fallbackHttp = new HttpClient(new SocketsHttpHandler
                    {
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
                    })
                    { Timeout = TimeSpan.FromSeconds(3) }; // Reduced from 15 to 3 seconds
            }
            
            LogInformation("TasmotaClient initialized without IP address");
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to initialize TasmotaClient without IP");
            throw;
        }
    }
    
    /// <summary>Convenience overload that matches the original API.</summary>
    public TasmotaClient(string ipAddress) : this(ipAddress, logger: null, httpClientFactory: null) { }

    /// <summary>Change device IP/host at runtime.</summary>
    public void SetIp(string ipOrHost)
    {
        try
        {
            _rootUrl = NormalizeRoot(ipOrHost);
            _baseUrl = $"{_rootUrl}/cm?cmnd=";
            LogInformation("Tasmota base URL set to {Url}", _rootUrl);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to set IP address to {IpOrHost}", ipOrHost);
            throw;
        }
    }

    private static string NormalizeRoot(string ipOrHost)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ipOrHost))
                throw new ArgumentException("IP/host cannot be empty.", nameof(ipOrHost));

            if (!ipOrHost.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                ipOrHost = $"http://{ipOrHost}";

            return ipOrHost.TrimEnd('/');
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid IP/host format: {ipOrHost}", nameof(ipOrHost), ex);
        }
    }
    
    // AP Mode açma/kapama
    public async Task<bool> SetAccessPointModeAsync(bool enable, CancellationToken ct = default)
    {
        try 
        { 
            await SendCommandAsync($"WifiConfig {(enable ? 2 : 4)}", ct); 
            return true; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetAccessPointModeAsync] Failed to set AP mode {Enable}", enable); 
            return false; 
        }
    }

// AP credentials ayarlama
    public async Task<bool> SetAccessPointCredentialsAsync(string apSsid, string apPassword, CancellationToken ct = default)
    {
        try
        {
            await SendCommandAsync($"AP {apSsid}", ct);
            await SendCommandAsync($"APPassword {apPassword}", ct);
            return true;
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetAccessPointCredentialsAsync] Failed to set AP credentials"); 
            return false; 
        }
    }

// WiFi bağlantı durumu kontrolü
    public async Task<string?> GetWifiModeAsync(CancellationToken ct = default)
    {
        try
        {
            using var doc = await SendCommandAsync("WifiConfig", ct);
            return doc?.RootElement.GetProperty("WifiConfig").GetString();
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[GetWifiModeAsync] Failed to get WiFi mode"); 
            return null; 
        }
    }
    public async Task<bool> SetWifiRecoveryModeAsync(WifiRecoveryMode mode)
    {
        try 
        { 
            await SendCommandAsync($"WifiConfig {(int)mode}"); 
            return true; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetWifiRecoveryModeAsync] Failed to set WiFi recovery mode {Mode}", mode); 
            return false; 
        }
    }

    private HttpClient Http => _httpClientFactory?.CreateClient("TasmotaSharp") ?? _fallbackHttp
        ?? throw new InvalidOperationException("No HttpClient available.");

    private void LogDebug(string msg, params object?[] args)
    {
        try
        {
            if (_logger != null)
                _logger.LogDebug(msg, args);
            else
                Console.WriteLine("[DBG] " + string.Format(System.Globalization.CultureInfo.InvariantCulture, msg, args));
        }
        catch
        {
            // Suppress logging errors
        }
    }

    private void LogInformation(string msg, params object?[] args)
    {
        try
        {
            if (_logger != null)
                _logger.LogInformation(msg, args);
            else
                Console.WriteLine("[INF] " + string.Format(System.Globalization.CultureInfo.InvariantCulture, msg, args));
        }
        catch
        {
            // Suppress logging errors
        }
    }

    private void LogWarning(string msg, params object?[] args)
    {
        try
        {
            if (_logger != null)
                _logger.LogWarning(msg, args);
            else
                Console.WriteLine("[WRN] " + string.Format(System.Globalization.CultureInfo.InvariantCulture, msg, args));
        }
        catch
        {
            // Suppress logging errors
        }
    }

    private void LogError(Exception ex, string msg, params object?[] args)
    {
        try
        {
            if (_logger != null)
                _logger.LogError(ex, msg, args);
            else
                Console.WriteLine("[ERR] " + string.Format(System.Globalization.CultureInfo.InvariantCulture, msg, args) + " :: " + ex);
        }
        catch
        {
            // Suppress logging errors
        }
    }

    #region Core HTTP helpers

    private async Task<JsonDocument?> SendCommandAsync(string command, CancellationToken ct = default)
    {
        try
        {
            var url = _baseUrl + Uri.EscapeDataString(command);
            LogDebug("Sending command: {Cmd} -> {Url}", command, url);
            
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            
            var s = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            LogDebug("Response ({Status}): {Body}", (int)res.StatusCode, Truncate(s, 800));
            
            return JsonOrNull(s);
        }
        catch (Exception ex)
        {
            LogError(ex, "[SendCommandAsync] Error while sending command {Cmd}", command);
            return null;
        }
    }

    private async Task<string?> SendCommandGetStringAsync(string command, CancellationToken ct = default)
    {
        try
        {
            var url = _baseUrl + Uri.EscapeDataString(command);
            LogDebug("GET string: {Cmd} -> {Url}", command, url);
            
            return await Http.GetStringAsync(url, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogError(ex, "[SendCommandGetStringAsync] Error {Cmd}", command);
            return null;
        }
    }

    private static JsonDocument? JsonOrNull(string? s)
    {
        try 
        { 
            return s is null ? null : JsonDocument.Parse(s); 
        }
        catch 
        { 
            return null; 
        }
    }

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "…");

    #endregion

    #region Relay controls

    /// <summary>Set relay n to ON/OFF (n = 1..8)</summary>
    public async Task<bool?> SetRelayAsync(int relay, bool state, CancellationToken ct = default)
    {
        try
        {
            using var doc = await SendCommandAsync($"Power{relay} {(state ? "ON" : "OFF")}", ct);
            if (doc == null) return null;
            
            var prop = doc.RootElement.EnumerateObject()
                .FirstOrDefault(p => p.Name.StartsWith($"POWER{relay}", StringComparison.OrdinalIgnoreCase));
            
            if (prop.Value.ValueKind == JsonValueKind.Undefined)
                return null;
                
            return string.Equals(prop.Value.GetString(), "ON", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            LogError(ex, "[SetRelayAsync] Failed for relay {Relay} state {State}", relay, state);
            return null;
        }
    }

    /// <summary>Toggle relay n (n = 1..8)</summary>
    public async Task<bool?> ToggleRelayAsync(int relay, CancellationToken ct = default)
    {
        try
        {
            using var doc = await SendCommandAsync($"Power{relay} TOGGLE", ct);
            if (doc == null) return null;
            
            var prop = doc.RootElement.EnumerateObject()
                .FirstOrDefault(p => p.Name.StartsWith($"POWER{relay}", StringComparison.OrdinalIgnoreCase));
            
            if (prop.Value.ValueKind == JsonValueKind.Undefined)
                return null;
                
            return string.Equals(prop.Value.GetString(), "ON", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            LogError(ex, "[ToggleRelayAsync] Failed for relay {Relay}", relay);
            return null;
        }
    }

    /// <summary>Get relay n state (n = 1..8)</summary>
    public async Task<bool?> GetRelayStateAsync(int relay, CancellationToken ct = default)
    {
        try
        {
            using var doc = await SendCommandAsync($"Power{relay}", ct);
            if (doc == null) return null;
            
            var prop = doc.RootElement.EnumerateObject()
                .FirstOrDefault(p => p.Name.StartsWith($"POWER{relay}", StringComparison.OrdinalIgnoreCase));
            
            if (prop.Value.ValueKind == JsonValueKind.Undefined)
                return null;
                
            return string.Equals(prop.Value.GetString(), "ON", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            LogError(ex, "[GetRelayStateAsync] Failed for relay {Relay}", relay);
            return null;
        }
    }

    #endregion

    #region Time / timezone / DST

    /// <summary>Read device time (Tasmota "Time")</summary>
    public async Task<DateTime?> GetTimeAsync(CancellationToken ct = default)
    {
        try
        {
            using var doc = await SendCommandAsync("Time", ct);
            if (doc == null) return null;
            
            if (!doc.RootElement.TryGetProperty("Time", out var timeProp))
                return null;
                
            var data = timeProp.GetString();
            return data is null ? null : DateTime.Parse(data);
        }
        catch (Exception ex)
        {
            LogError(ex, "[GetTimeAsync] Failed to get device time");
            return null;
        }
    }

    /// <summary>Set device time (also writes to RTC when present)</summary>
    public async Task<bool> SetTimeAsync(DateTime dateTime, CancellationToken ct = default)
    {
        try 
        { 
            _ = await SendCommandAsync($"Time {dateTime:yyyy-MM-dd HH:mm:ss}", ct); 
            return true; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetTimeAsync] Failed to set time to {DateTime}", dateTime); 
            return false; 
        }
    }

    /// <summary>Set timezone offset (e.g. TR = 3)</summary>
    public async Task<bool> SetTimezoneAsync(int offset, CancellationToken ct = default)
    {
        try 
        { 
            _ = await SendCommandAsync($"Timezone {offset}", ct); 
            return true; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetTimezoneAsync] Failed to set timezone offset {Offset}", offset); 
            return false; 
        }
    }

    /// <summary>Enable/disable DST (commonly SetOption36 in many builds)</summary>
    public async Task<bool> SetDstAsync(bool enabled, CancellationToken ct = default)
    {
        try 
        { 
            _ = await SendCommandAsync($"SetOption36 {(enabled ? 1 : 0)}", ct); 
            return true; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetDstAsync] Failed to set DST to {Enabled}", enabled); 
            return false; 
        }
    }

    #endregion

    #region Status

    /// <summary>Full status (Status 0) mapped to TasmotaStatus model (loose types)</summary>
    public async Task<TasmotaStatus?> GetStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var s = await SendCommandGetStringAsync("Status 0", ct);
            if (s == null) return null;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
            
            return JsonSerializer.Deserialize<TasmotaStatus>(s, options);
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[GetStatusAsync] Failed to get device status"); 
            return null; 
        }
    }

    #endregion

    #region mDNS

    /// <summary>Enable/disable mDNS (often SetOption55)</summary>
    public async Task<bool> EnableMdnsAsync(bool enable, CancellationToken ct = default)
    {
        try 
        { 
            _ = await SendCommandAsync($"SetOption55 {(enable ? 1 : 0)}", ct); 
            return true; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[EnableMdnsAsync] Failed to set mDNS to {Enable}", enable); 
            return false; 
        }
    }

    /// <summary>Get mDNS state; true=ON, false=OFF, null=unknown</summary>
    public async Task<bool?> GetMdnsStateAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await SendCommandGetStringAsync("SetOption55", ct);
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
            LogError(ex, "[GetMdnsStateAsync] Failed to get mDNS state"); 
            return null; 
        }
    }

    #endregion

    #region Restart / Factory reset

    /// <summary>Soft restart</summary>
    public async Task<bool> RestartAsync(CancellationToken ct = default)
    {
        try 
        { 
            _ = await SendCommandAsync("Restart 1", ct); 
            return true; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[RestartAsync] Failed to restart device"); 
            return false; 
        }
    }

    /// <summary>Factory reset; mode: 1,2,5 (1 basic, 2 keep Wi-Fi/MQTT, 5 safest)</summary>
    public async Task<bool> FactoryResetAsync(int mode, CancellationToken ct = default)
    {
        try
        {
            if (mode is not (1 or 2 or 5))
                throw new ArgumentOutOfRangeException(nameof(mode), "Reset mode must be 1, 2, or 5.");
                
            _ = await SendCommandAsync($"Reset {mode}", ct);
            return true;
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[FactoryResetAsync] Failed to factory reset with mode {Mode}", mode); 
            return false; 
        }
    }

    #endregion

    #region Config backup / restore

    /// <summary>Download config dump (/dl)</summary>
    public async Task<byte[]?> BackupConfigAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"{_rootUrl}/dl";
            LogDebug("Downloading backup from {Url}", url);
            
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            
            if (!res.IsSuccessStatusCode)
            {
                LogWarning("Backup download failed with {Status}", (int)res.StatusCode);
                return null;
            }
            
            var bytes = await res.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            LogInformation("Backup downloaded: {Bytes} bytes", bytes.Length);
            return bytes;
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[BackupConfigAsync] Failed to download config backup"); 
            return null; 
        }
    }

    /// <summary>Upload config dump (/u)</summary>
    public async Task<bool> RestoreConfigAsync(byte[] dmpData, CancellationToken ct = default)
    {
        try
        {
            var url = $"{_rootUrl}/u";
            using var content = new MultipartFormDataContent
            {
                { new ByteArrayContent(dmpData), "file", "config.dmp" }
            };
            
            using var res = await Http.PostAsync(url, content, ct).ConfigureAwait(false);
            var ok = res.IsSuccessStatusCode;
            
            if (!ok) 
                LogWarning("Restore returned {Status}", (int)res.StatusCode);
                
            return ok;
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[RestoreConfigAsync] Failed to restore config backup"); 
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
        bool reconnect = true,
        CancellationToken ct = default)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(host))      _ = await SendCommandAsync($"MqttHost {host}", ct);
            if (port.HasValue)                         _ = await SendCommandAsync($"MqttPort {port.Value}", ct);
            if (!string.IsNullOrWhiteSpace(user))      _ = await SendCommandAsync($"MqttUser {user}", ct);
            if (!string.IsNullOrWhiteSpace(password))  _ = await SendCommandAsync($"MqttPassword {password}", ct);
            if (!string.IsNullOrWhiteSpace(clientId))  _ = await SendCommandAsync($"MqttClient {clientId}", ct);
            if (!string.IsNullOrWhiteSpace(topic))     _ = await SendCommandAsync($"Topic {topic}", ct);
            if (!string.IsNullOrWhiteSpace(fullTopic)) _ = await SendCommandAsync($"FullTopic {fullTopic}", ct);
            if (reconnect)                             _ = await SendCommandAsync("MqttReconnect 1", ct);
            
            return true;
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetMqttAsync] Failed to set MQTT configuration"); 
            return false; 
        }
    }

    /// <summary>Read StatusMQT block from Status 0</summary>
    public async Task<StatusMQT?> GetMqttStatusAsync(CancellationToken ct = default)
    {
        try 
        { 
            return (await GetStatusAsync(ct))?.StatusMQT; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[GetMqttStatusAsync] Failed to get MQTT status"); 
            return null; 
        }
    }

    #endregion

    #region Wi-Fi

    /// <summary>Set SSID/PW (optionally restart)</summary>
    public async Task<bool> SetWifiCredentialsAsync(string ssid, string password, bool restartAfter = true, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ssid))
                throw new ArgumentException("SSID cannot be empty.", nameof(ssid));

            _ = await SendCommandAsync($"SSID1 {ssid}", ct);
            _ = await SendCommandAsync($"Password1 {password}", ct);
            _ = await SendCommandAsync("WifiConfig 4", ct); // fail -> AP+Config

            if (restartAfter)
                _ = await RestartAsync(ct);

            return true;
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetWifiCredentialsAsync] Failed to set WiFi credentials for SSID {Ssid}", ssid); 
            return false; 
        }
    }

    /// <summary>Get Wi-Fi info (StatusNET + StatusSTS.Wifi)</summary>
    public async Task<(StatusNET? Net, Wifi? Wifi)?> GetWifiInfoAsync(CancellationToken ct = default)
    {
        try
        {
            var st = await GetStatusAsync(ct);
            if (st == null) return null;
            return (st.StatusNET, st.StatusSTS?.Wifi);
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[GetWifiInfoAsync] Failed to get WiFi info"); 
            return null; 
        }
    }

    /// <summary>Wi-Fi scan (WifiScan 1 + poll WifiScan)</summary>
    public async Task<List<WifiScanResult>?> ScanWifiAsync(int timeoutMs = 5000, int pollIntervalMs = 400, CancellationToken ct = default)
    {
        try
        {
            _ = await SendCommandGetStringAsync("WifiScan 1", ct);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                await Task.Delay(pollIntervalMs, ct).ConfigureAwait(false);
                var raw = await SendCommandGetStringAsync("WifiScan", ct);
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
            LogError(ex, "[ScanWifiAsync] Failed to scan WiFi networks"); 
            return null; 
        }
    }

    #endregion

    #region LED helpers

    /// <summary>Set LED state mode (0..8)</summary>
    public async Task<bool> SetLedStateAsync(int mode /*0..8*/, CancellationToken ct = default)
    {
        try 
        { 
            _ = await SendCommandAsync($"LedState {mode}", ct); 
            return true; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetLedStateAsync] Failed to set LED state to mode {Mode}", mode); 
            return false; 
        }
    }

    /// <summary>Set LedPowerN ON/OFF (if present)</summary>
    public async Task<bool?> SetLedPowerAsync(int index, bool on, CancellationToken ct = default)
    {
        try
        {
            using var doc = await SendCommandAsync($"LedPower{index} {(on ? "ON" : "OFF")}", ct);
            if (doc == null) return null;
            
            var prop = doc.RootElement.EnumerateObject()
                .FirstOrDefault(p => p.Name.StartsWith($"LedPower{index}", StringComparison.OrdinalIgnoreCase));
            
            if (prop.Value.ValueKind == JsonValueKind.Undefined)
                return null;
                
            return string.Equals(prop.Value.GetString(), "ON", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetLedPowerAsync] Failed to set LED power {Index} to {State}", index, on); 
            return null; 
        }
    }

    #endregion

    #region Sensors / telemetry

    /// <summary>Status 10 mapped to TasmotaSensorStatus (e.g., DS3231)</summary>
    public async Task<TasmotaSensorStatus?> GetSensorStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await SendCommandGetStringAsync("Status 10", ct);
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
            LogError(ex, "[GetSensorStatusAsync] Failed to get sensor status"); 
            return null; 
        }
    }

    /// <summary>Set telemetry period (seconds)</summary>
    public async Task<bool> SetTelePeriodAsync(int seconds, CancellationToken ct = default)
    {
        try 
        { 
            _ = await SendCommandAsync($"TelePeriod {seconds}", ct); 
            return true; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetTelePeriodAsync] Failed to set telemetry period to {Seconds} seconds", seconds); 
            return false; 
        }
    }

    #endregion

    #region Timers (weekly schedule)

    // Tasmota "Days" field is 7 chars for Sun..Sat (S M T W T F S). '-'=off, any char=on.
    private static string BuildDaysMask(params DayOfWeek[] days)
    {
        try
        {
            var mask = new[] { '-', '-', '-', '-', '-', '-', '-' };
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
        catch
        {
            return "-------"; // Default mask if error
        }
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
        int window = 0,
        CancellationToken ct = default)
    {
        try
        {
            if (index < 1 || index > 16) 
                throw new ArgumentOutOfRangeException(nameof(index), "Timer index must be 1-16");
                
            var daysMask = BuildDaysMask(days?.ToArray() ?? Array.Empty<DayOfWeek>());
            var hhmm = time.ToString("HH\\:mm");
            var payload = $"{{\"Enable\":1,\"Time\":\"{hhmm}\",\"Window\":{window},\"Days\":\"{daysMask}\",\"Repeat\":{(repeat ? 1 : 0)},\"Output\":{output},\"Action\":{action},\"Mode\":{mode}}}";
            
            _ = await SendCommandAsync($"Timer{index} {payload}", ct);
            _ = await SendCommandAsync("Timers 1", ct); // ensure timers subsystem is on
            
            return true;
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetTimerAsync] Failed to set timer {Index}", index); 
            return false; 
        }
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
        int window = 0,
        CancellationToken ct = default)
    {
        try
        {
            if (index < 1 || index > 16) 
                throw new ArgumentOutOfRangeException(nameof(index), "Timer index must be 1-16");
                
            var payload = $"{{\"Enable\":1,\"Time\":\"{timeHHmm}\",\"Window\":{window},\"Days\":\"{daysMask}\",\"Repeat\":{(repeat ? 1 : 0)},\"Output\":{output},\"Action\":{action},\"Mode\":{mode}}}";
            
            _ = await SendCommandAsync($"Timer{index} {payload}", ct);
            _ = await SendCommandAsync("Timers 1", ct);
            
            return true;
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetTimerByMaskAsync] Failed to set timer {Index} with mask {Mask}", index, daysMask); 
            return false; 
        }
    }

    public async Task<bool> EnableAllTimersAsync(bool enable, CancellationToken ct = default)
    {
        try 
        { 
            _ = await SendCommandAsync($"Timers {(enable ? 1 : 0)}", ct); 
            return true; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[EnableAllTimersAsync] Failed to {Action} all timers", enable ? "enable" : "disable"); 
            return false; 
        }
    }

    public async Task<bool> DisableTimerAsync(int index, CancellationToken ct = default)
    {
        try 
        { 
            _ = await SendCommandAsync($"Timer{index} 0", ct); 
            return true; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[DisableTimerAsync] Failed to disable timer {Index}", index); 
            return false; 
        }
    }

    /// <summary>Disable and clear TimerN payload</summary>
    public async Task<bool> ClearTimerAsync(int index, CancellationToken ct = default)
    {
        try
        {
            _ = await SendCommandAsync($"Timer{index} 0", ct);
            _ = await SendCommandAsync($"Timer{index} {{}}", ct);
            return true;
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[ClearTimerAsync] Failed to clear timer {Index}", index); 
            return false; 
        }
    }

    #endregion

    #region Geo (sunrise/sunset)

    public async Task<bool> SetGeoAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        try
        {
            _ = await SendCommandAsync($"Latitude {latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}", ct);
            _ = await SendCommandAsync($"Longitude {longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}", ct);
            return true;
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetGeoAsync] Failed to set geo coordinates {Lat},{Lon}", latitude, longitude); 
            return false; 
        }
    }

    #endregion

    #region Rules (one-shot, relative pulse, sunrise/sunset) + management

    /// <summary>
    /// One-shot absolute date/time rule:
    /// at 'whenLocal' do Power{output} on/off, optional pulse then revert.
    /// Internally uses StatusTIM#Local pattern + Time#Minute + Status 7 helper.
    /// </summary>
    public async Task<bool> SetOneShotDateRuleAsync(
        int ruleIndex,
        DateTime whenLocal,
        int output,
        bool onWhenTrue,
        TimeSpan? pulse = null,
        CancellationToken ct = default)
    {
        try
        {
            if (ruleIndex is < 1 or > 3) 
                throw new ArgumentOutOfRangeException(nameof(ruleIndex), "Rule index must be 1-3");

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
            _ = await SendCommandAsync(ruleCmd, ct);

            int minuteOfDay = whenLocal.Hour * 60 + whenLocal.Minute;
            int aux = ruleIndex == 3 ? 1 : ruleIndex + 1;
            string auxRuleCmd = $"Rule{aux} on Time#Minute={minuteOfDay} do Status 7 endon";

            _ = await SendCommandAsync(auxRuleCmd, ct);
            _ = await SendCommandAsync($"Rule{ruleIndex} 1", ct);
            _ = await SendCommandAsync($"Rule{aux} 1", ct);
            
            return true;
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetOneShotDateRuleAsync] Failed to set one-shot rule {RuleIndex}", ruleIndex); 
            return false; 
        }
    }

    public async Task<bool> EnableRuleAsync(int ruleIndex, bool enable, CancellationToken ct = default)
    {
        try 
        { 
            _ = await SendCommandAsync($"Rule{ruleIndex} {(enable ? 1 : 0)}", ct); 
            return true; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[EnableRuleAsync] Failed to {Action} rule {RuleIndex}", enable ? "enable" : "disable", ruleIndex); 
            return false; 
        }
    }

    public async Task<bool> SetRuleScriptAsync(int ruleIndex, string script, CancellationToken ct = default)
    {
        try 
        { 
            _ = await SendCommandAsync($"Rule{ruleIndex} {script}", ct); 
            return true; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[SetRuleScriptAsync] Failed to set rule {RuleIndex} script", ruleIndex); 
            return false; 
        }
    }

    /// <summary>Disable and clear rule script</summary>
    public async Task<bool> ClearRuleAsync(int ruleIndex, CancellationToken ct = default)
    {
        try
        {
            _ = await SendCommandAsync($"Rule{ruleIndex} 0", ct);
            _ = await SendCommandAsync($"Rule{ruleIndex} \"\"", ct);
            return true;
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[ClearRuleAsync] Failed to clear rule {RuleIndex}", ruleIndex); 
            return false; 
        }
    }

    /// <summary>Start RuleTimerN countdown in seconds (for relative pulse)</summary>
    public async Task<bool> StartRuleTimerAsync(int index, int seconds, CancellationToken ct = default)
    {
        try 
        { 
            _ = await SendCommandAsync($"RuleTimer{index} {seconds}", ct); 
            return true; 
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[StartRuleTimerAsync] Failed to start rule timer {Index} for {Seconds} seconds", index, seconds); 
            return false; 
        }
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
        int ruleIndex = 1,
        CancellationToken ct = default)
    {
        try
        {
            var whenLocal = DateTime.Now.Add(delay);
            return await SetOneShotDateRuleAsync(ruleIndex, whenLocal, output, onWhenTrue, pulse, ct);
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[ScheduleOneShotInAsync] Failed to schedule one-shot in {Delay}", delay); 
            return false; 
        }
    }

    private static bool OnOffToBool(string? s) => string.Equals(s, "ON", StringComparison.OrdinalIgnoreCase);

    /// <summary>Read RuleN state/script; supports both nested and flat JSON variants</summary>
    public async Task<RuleInfo?> GetRuleInfoAsync(int ruleIndex, CancellationToken ct = default)
    {
        try
        {
            var raw = await SendCommandGetStringAsync($"Rule{ruleIndex}", ct);
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
            LogError(ex, "[GetRuleInfoAsync] Failed to get rule {RuleIndex} info", ruleIndex);
            return null;
        }
    }

    public async Task<List<RuleInfo>> GetAllRulesAsync(CancellationToken ct = default)
    {
        try
        {
            var list = new List<RuleInfo>();
            for (int i = 1; i <= 3; i++)
            {
                var ri = await GetRuleInfoAsync(i, ct);
                if (ri != null) list.Add(ri);
            }
            return list;
        }
        catch (Exception ex)
        {
            LogError(ex, "[GetAllRulesAsync] Failed to get all rules");
            return new List<RuleInfo>();
        }
    }

    /// <summary>Disable + clear script (alias of ClearRuleAsync)</summary>
    public async Task<bool> DeleteRuleAsync(int ruleIndex, CancellationToken ct = default) => await ClearRuleAsync(ruleIndex, ct);

    /// <summary>
    /// Relative pulse in one call: after startDelaySeconds -> ON, after pulseSeconds -> OFF.
    /// Uses RuleTimer + Rules#Timer event.
    /// </summary>
    public async Task<bool> PulseAfterAsync(int startDelaySeconds, int output, int pulseSeconds, int ruleIndex = 1, int timerIndex = 1, CancellationToken ct = default)
    {
        try
        {
            _ = await SetRuleScriptAsync(ruleIndex,
                $"on Rules#Timer={timerIndex} do backlog Power{output} 1; Delay {pulseSeconds * 10}; Power{output} 0 endon",
                ct);
            _ = await EnableRuleAsync(ruleIndex, true, ct);
            _ = await StartRuleTimerAsync(timerIndex, startDelaySeconds, ct);
            return true;
        }
        catch (Exception ex) 
        { 
            LogError(ex, "[PulseAfterAsync] Failed to set pulse after {StartDelay}s", startDelaySeconds); 
            return false; 
        }
    }

    #endregion

    #region Simple rule model + builder (high-level API)

    private static string BuildBacklogPulse(int output, bool onWhenTrue, int? pulseSeconds, bool autoDisable, int ruleIndex)
    {
        try
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
        catch
        {
            return $"Power{output} {(onWhenTrue ? 1 : 0)}"; // Fallback
        }
    }

    private static (string rule1, string? rule2) BuildRuleScripts(SimpleRule s, int ruleIndex, int timerIndex = 1)
    {
        try
        {
            switch (s.Type)
            {
                case SimpleRuleType.OneShotAtLocalTime:
                {
                    if (s.WhenLocal == null) throw new ArgumentException("WhenLocal cannot be null.");
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
                        throw new ArgumentException("StartDelaySeconds and PulseSeconds are required.");
                    var action = BuildBacklogPulse(s.Output, s.OnWhenTrue, s.PulseSeconds, s.AutoDisable, ruleIndex);
                    string ruleA = $"on Rules#Timer={timerIndex} do {action} endon";
                    return (ruleA, null);
                }

                case SimpleRuleType.SunriseSunset:
                {
                    if (s.UseSunset is null) throw new ArgumentException("UseSunset is required.");
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
        catch
        {
            return ("", null); // Fallback empty rule
        }
    }

    /// <summary>Compile and apply SimpleRule; writes RuleN (and helper) and enables them.</summary>
    public async Task<bool> ApplySimpleRuleAsync(int ruleIndex, SimpleRule rule, int timerIndexForRelative = 1, CancellationToken ct = default)
    {
        try
        {
            var (r1, r2) = BuildRuleScripts(rule, ruleIndex, timerIndexForRelative);

            _ = await SetRuleScriptAsync(ruleIndex, r1, ct);
            _ = await EnableRuleAsync(ruleIndex, true, ct);

            if (!string.IsNullOrEmpty(r2))
            {
                int aux = ruleIndex == 3 ? 1 : ruleIndex + 1;
                _ = await SetRuleScriptAsync(aux, r2!, ct);
                _ = await EnableRuleAsync(aux, true, ct);
            }

            if (rule.Type == SimpleRuleType.RelativePulse && rule.StartDelaySeconds is int sec)
            {
                _ = await StartRuleTimerAsync(timerIndexForRelative, sec, ct);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError(ex, "[ApplySimpleRuleAsync] Failed to apply simple rule {RuleIndex}", ruleIndex);
            return false;
        }
    }

    /// <summary>Best-effort parse of a rule script into a SimpleRule model.</summary>
    public async Task<SimpleRule?> GetSimpleRuleAsync(int ruleIndex, CancellationToken ct = default)
    {
        try
        {
            var info = await GetRuleInfoAsync(ruleIndex, ct);
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
                    // StartDelaySeconds not known (Tasmota doesn't expose last RuleTimer value)
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
        catch (Exception ex)
        {
            LogError(ex, "[GetSimpleRuleAsync] Failed to parse simple rule {RuleIndex}", ruleIndex);
            return null;
        }
    }

    #endregion

    #region Multi-output schedule helper

    /// <summary>
    /// Program multiple relays to turn ON/OFF at the same times on the same days.
    /// Strategy:
    ///   - Timers: uses native Timer slots (2 per relay: ON + OFF)
    ///   - RuleBacklog: uses rule triggers at minute-of-day (no day mask unless you extend the rule)
    /// </summary>
    /// <param name="days">Day list (e.g., Monday..Wednesday)</param>
    /// <param name="onTimeHHmm">"HH:mm" 24h (e.g., "09:00")</param>
    /// <param name="offTimeHHmm">"HH:mm" 24h (e.g., "22:00")</param>
    /// <param name="outputs">Relay list (1..8)</param>
    /// <param name="strategy">Timers or RuleBacklog</param>
    /// <param name="startTimerIndex">For Timers strategy: first timer slot to use (default 1). Needs 2 * outputs.Count slots.</param>
    /// <param name="ruleIndex">For RuleBacklog strategy: which Rule slot (1..3) to use.</param>
    public async Task<bool> SetTimerMultiAsync(
        IEnumerable<DayOfWeek> days,
        string onTimeHHmm,
        string offTimeHHmm,
        IEnumerable<int> outputs,
        MultiScheduleStrategy strategy = MultiScheduleStrategy.Timers,
        int startTimerIndex = 1,
        int ruleIndex = 1,
        CancellationToken ct = default)
    {
        try
        {
            if (outputs is null) throw new ArgumentNullException(nameof(outputs));
            var outs = outputs.ToArray();
            if (outs.Length == 0) throw new ArgumentException("At least one relay is required.", nameof(outputs));
            if (outs.Any(o => o < 1 || o > 8)) throw new ArgumentOutOfRangeException(nameof(outputs), "Relay IDs must be 1..8.");

            static int MinuteOfDay(string hhmm)
            {
                if (TimeSpan.TryParseExact(hhmm, "HH\\:mm", null, out var t) ||
                    TimeSpan.TryParse(hhmm, out t))
                    return t.Hours * 60 + t.Minutes;
                throw new FormatException($"Invalid time format: {hhmm}. Expected HH:mm.");
            }

            if (strategy == MultiScheduleStrategy.Timers)
            {
                int need = outs.Length * 2;
                if (startTimerIndex < 1 || startTimerIndex + need - 1 > 16)
                    throw new InvalidOperationException($"Not enough timer slots. start={startTimerIndex}, need={need}, limit=16.");

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
                        action: 1,   // ON
                        ct: ct
                    );
                    if (!ok1) return false;

                    // OFF
                    bool ok2 = await SetTimerByMaskAsync(
                        index: timer++,
                        daysMask: BuildDaysMask(daysArr),
                        timeHHmm: offTimeHHmm,
                        output: o,
                        action: 0,   // OFF
                        ct: ct
                    );
                    if (!ok2) return false;
                }

                _ = await EnableAllTimersAsync(true, ct);
                return true;
            }
            else // RuleBacklog
            {
                if (ruleIndex is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(ruleIndex), "Rule index must be 1..3.");

                int onMinute  = MinuteOfDay(onTimeHHmm);
                int offMinute = MinuteOfDay(offTimeHHmm);

                // NOTE: Day-specific rule masking is complex; for strict day control prefer Timers strategy.
                string onBacklog  = string.Join("; ", outs.Select(o => $"Power{o} ON"));
                string offBacklog = string.Join("; ", outs.Select(o => $"Power{o} OFF"));

                string ruleScript =
                    $"on Time#Minute={onMinute} do Backlog {onBacklog} endon " +
                    $"on Time#Minute={offMinute} do Backlog {offBacklog} endon";

                if (!await SetRuleScriptAsync(ruleIndex, ruleScript, ct)) return false;
                return await EnableRuleAsync(ruleIndex, true, ct);
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "[SetTimerMultiAsync] Failed to set multi-timer schedule");
            return false;
        }
    }

    #endregion
    
    public async Task<int?> GetRelayCountAsync(CancellationToken ct = default)
    {
        try
        {
            var raw = await SendCommandGetStringAsync("Status 11", ct);
            if (string.IsNullOrWhiteSpace(raw)) return null;

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("StatusPWR", out var pwr))
            {
                return pwr.EnumerateObject()
                    .Count(p => p.Name.StartsWith("POWER", StringComparison.OrdinalIgnoreCase));
            }

            // fallback: StatusSTS kontrol et
            if (doc.RootElement.TryGetProperty("StatusSTS", out var sts))
            {
                return sts.EnumerateObject()
                    .Count(p => p.Name.StartsWith("POWER", StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }
        catch (Exception ex)
        {
            LogError(ex, "[GetRelayCountAsync] Failed to get relay count");
            return null;
        }
    }

    public void Dispose()
    {
        try
        {
            // Dispose fallback client only if we created it
            _fallbackHttp?.Dispose();
            LogDebug("TasmotaClient disposed successfully");
        }
        catch (Exception ex)
        {
            LogError(ex, "[Dispose] Error during disposal");
        }
    }
}