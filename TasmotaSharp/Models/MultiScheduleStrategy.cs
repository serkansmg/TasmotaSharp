namespace TasmotaSharp.Models;

public enum MultiScheduleStrategy
{
    Timers,      // Her röle için ayrı Timer
    RuleBacklog  // Tek Rule içinde toplu Backlog
}