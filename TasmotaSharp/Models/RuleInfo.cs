namespace TasmotaSharp.Models;

public class RuleInfo
{
    public int Index { get; set; }
    public bool Enabled { get; set; }
    public bool Once { get; set; }
    public bool StopOnError { get; set; }
    public int? Free { get; set; }
    public int? Length { get; set; }
    public string? Script { get; set; }
}