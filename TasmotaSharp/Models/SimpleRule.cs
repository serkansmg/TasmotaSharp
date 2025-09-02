using System;

namespace TasmotaSharp.Models;
/// <summary>Simple, structured rule types that compile into Tasmota rule scripts.</summary>
public enum SimpleRuleType
{
    OneShotAtLocalTime,
    RelativePulse,
    SunriseSunset,
    Unknown
}

public class SimpleRule
{
    public SimpleRuleType Type { get; set; }

    // Common
    public int Output { get; set; } = 1;          // Relay1..8
    public bool OnWhenTrue { get; set; } = true;
    public bool AutoDisable { get; set; } = false; // Append "RuleN 0" to action

    // OneShotAtLocalTime
    public DateTime? WhenLocal { get; set; }
    public TimeSpan? Pulse { get; set; }

    // RelativePulse
    public int? StartDelaySeconds { get; set; }
    public int? PulseSeconds { get; set; }

    // Sunrise/Sunset
    public bool? UseSunset { get; set; }          // true=sunset, false=sunrise
    public int? OffsetMinutes { get; set; }       // optional +/- minutes
}
