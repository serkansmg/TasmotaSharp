namespace TasmotaSharp.Models;

public class WifiScanResult
{
    public string? SSID { get; set; }
    public string? BSSId { get; set; }
    public int? Channel { get; set; }
    public int? SignalDbm { get; set; }
    public int? RssiPercent { get; set; }
    public string? Encryption { get; set; }
}