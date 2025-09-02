using System.Text.Json.Serialization;

namespace TasmotaSharp.Models;

#region ===== Models (minimal, flexible) =====

 

public class TasmotaStatus
{
    public Status? Status { get; set; }
    public StatusPRM? StatusPRM { get; set; }
    public StatusFWR? StatusFWR { get; set; }
    public StatusLOG? StatusLOG { get; set; }
    public StatusMEM? StatusMEM { get; set; }
    public StatusNET? StatusNET { get; set; }
    public StatusMQT? StatusMQT { get; set; }
    public StatusTIM? StatusTIM { get; set; }
    public StatusSNS? StatusSNS { get; set; }
    public StatusSTS? StatusSTS { get; set; }
}

public class Status
{
    public int Module { get; set; }
    public string? DeviceName { get; set; }
    public string[]? FriendlyName { get; set; }
    public string? Topic { get; set; }
    public string? ButtonTopic { get; set; }
    public string? Power { get; set; }
    public string? PowerLock { get; set; }
    public int PowerOnState { get; set; }
    public int LedState { get; set; }
    public string? LedMask { get; set; }
    public int SaveData { get; set; }
    public int SaveState { get; set; }
    public string? SwitchTopic { get; set; }
    public int[]? SwitchMode { get; set; }
    public int ButtonRetain { get; set; }
    public int SwitchRetain { get; set; }
    public int SensorRetain { get; set; }
    public int PowerRetain { get; set; }
    public int InfoRetain { get; set; }
    public int StateRetain { get; set; }
    public int StatusRetain { get; set; }
}

public class StatusPRM
{
    public int Baudrate { get; set; }
    public string? SerialConfig { get; set; }
    public string? GroupTopic { get; set; }
    public string? OtaUrl { get; set; }
    public string? RestartReason { get; set; }
    public string? Uptime { get; set; }
    public string? StartupUTC { get; set; }
    public int Sleep { get; set; }
    public int CfgHolder { get; set; }
    public int BootCount { get; set; }
    public string? BCResetTime { get; set; }
    public int SaveCount { get; set; }
}

public class StatusFWR
{
    public string? Version { get; set; }
    public string? BuildDateTime { get; set; }
    public string? Core { get; set; }
    public string? SDK { get; set; }
    public int CpuFrequency { get; set; }
    public string? Hardware { get; set; }
    public string? CR { get; set; }
}

public class StatusLOG
{
    public int SerialLog { get; set; }
    public int WebLog { get; set; }
    public int MqttLog { get; set; }
    public int FileLog { get; set; }
    public int SysLog { get; set; }
    public string? LogHost { get; set; }
    public int LogPort { get; set; }
    public string[]? SSId { get; set; }
    public int TelePeriod { get; set; }
    public string? Resolution { get; set; }
    public string[]? SetOption { get; set; }
}

public class StatusMEM
{
    public int ProgramSize { get; set; }
    public int Free { get; set; }
    public int Heap { get; set; }
    public int StackLowMark { get; set; }
    public int PsrMax { get; set; }
    public int PsrFree { get; set; }
    public int ProgramFlashSize { get; set; }
    public int FlashSize { get; set; }
    public string? FlashChipId { get; set; }
    public int FlashFrequency { get; set; }
    public string? FlashMode { get; set; }
    public string[]? Features { get; set; }
    public string? Drivers { get; set; }
    public string? Sensors { get; set; }
    public string? I2CDriver { get; set; }
}

public class StatusNET
{
    public string? Hostname { get; set; }
    public string? IPAddress { get; set; }
    public string? Gateway { get; set; }
    public string? Subnetmask { get; set; }
    public string? DNSServer1 { get; set; }
    public string? DNSServer2 { get; set; }
    public string? Mac { get; set; }
    public string? IP6Global { get; set; }
    public string? IP6Local { get; set; }
    public Ethernet? Ethernet { get; set; }
    public int Webserver { get; set; }
    public int HTTP_API { get; set; }
    public int WifiConfig { get; set; }
    public double WifiPower { get; set; } // can be double in some builds
}

public class Ethernet
{
    public string? Hostname { get; set; }
    public string? IPAddress { get; set; }
    public string? Gateway { get; set; }
    public string? Subnetmask { get; set; }
    public string? DNSServer1 { get; set; }
    public string? DNSServer2 { get; set; }
    public string? Mac { get; set; }
    public string? IP6Global { get; set; }
    public string? IP6Local { get; set; }
}

public class StatusMQT
{
    public string? MqttHost { get; set; }
    public int MqttPort { get; set; }
    public string? MqttClientMask { get; set; }
    public string? MqttClient { get; set; }
    public string? MqttUser { get; set; }
    public int MqttCount { get; set; }
    public int MqttTLS { get; set; }
    public int MAX_PACKET_SIZE { get; set; }
    public int KEEPALIVE { get; set; }
    public int SOCKET_TIMEOUT { get; set; }
}

public class StatusTIM
{
    public string? UTC { get; set; }
    public string? Local { get; set; }
    public string? StartDST { get; set; }
    public string? EndDST { get; set; }
    public string? Timezone { get; set; }
    public string? Sunrise { get; set; }
    public string? Sunset { get; set; }
}

public class StatusSNS
{
    public string? Time { get; set; }
}

public class StatusSTS
{
    public string? Time { get; set; }
    public string? Uptime { get; set; }
    public int UptimeSec { get; set; }
    public int Heap { get; set; }
    public string? SleepMode { get; set; }
    public int Sleep { get; set; }
    public int LoadAvg { get; set; }
    public int MqttCount { get; set; }
    public Berry? Berry { get; set; }
    public string? POWER1 { get; set; }
    public string? POWER2 { get; set; }
    public Wifi? Wifi { get; set; }
    public string? Hostname { get; set; }
    public string? IPAddress { get; set; }
}

public class Berry
{
    public int HeapUsed { get; set; }
    public int Objects { get; set; }
}

public class Wifi
{
    public int AP { get; set; }
    public string? SSId { get; set; }
    public string? BSSId { get; set; }
    public int Channel { get; set; }
    public string? Mode { get; set; }
    public int RSSI { get; set; }
    public int Signal { get; set; }
    public int LinkCount { get; set; }
    public string? Downtime { get; set; }
}






public class SensorStatusSNS
{
    public DateTime Time { get; set; }
    public DS3231? DS3231 { get; set; }
    public string? TempUnit { get; set; }
}

public class DS3231
{
    public double Temperature { get; set; }
}

#endregion
