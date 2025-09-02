// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using TasmotaSharp;
using TasmotaSharp.Models;

Console.WriteLine("Hello, World!");
var builder = new HostApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("Application", "ConsoleApp")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/consoleapp-.txt",
        outputTemplate:
        "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
        rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddSingleton<TasmotaClient>();
builder.Services.AddHttpClient();
var app = builder.Build();

var scope = app.Services.CreateScope();
var client = scope.ServiceProvider.GetRequiredService<TasmotaClient>();

client.SetIp("10.0.4.41");

var sensorstatus = await client.GetSensorStatusAsync();


// var scanresults = await client.ScanWifiAsync();
// await client.EnableMdnsAsync(true);
// var mdns = await client.GetMdnsStateAsync();
// Console.WriteLine($"mDNS: {(mdns == true ? "ON" : mdns == false ? "OFF" : "Unknown")}");
//
// // Restart
// await client.RestartAsync();
// Türkiye için sabit UTC+3
//await client.SetTimezoneAsync(3);

// DST kapalı (Türkiye’de yaz/kış saati yok)
//await client.SetDstAsync(false);

//await client.SetTimeAsync(DateTime.Now);
// var backup = await client.BackupConfigAsync();
// if (backup != null)
// {
//     await File.WriteAllBytesAsync("tasmota_config.dmp", backup);
//     Console.WriteLine("Backup alındı.");
// }
// var time = await client.GetTimeAsync();
// Console.WriteLine($"Tasmota saati: {time}");
//
// var status= await client.GetStatusAsync();
//
// var state1= await client.GetRelayStateAsync(1);
// Console.WriteLine($"Röle 1 durumu: {state1}");
// // Röle 1 aç
// var setresult1=await client.SetRelayAsync(1, false);
var rules = await client.GetSimpleRuleAsync(1);

// var result=await client.ApplySimpleRuleAsync(1, new SimpleRule {
//     Type = SimpleRuleType.RelativePulse,
//     Output = 1,
//     OnWhenTrue = true,
//     StartDelaySeconds = 10,
//     PulseSeconds = 10,
//     AutoDisable = true
// });

// await client.SetTimerAsync(
//     index: 1,
//     time: new TimeSpan(9, 0, 0),
//     days: new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday },
//     output: 1,          // Röle 1
//     action: 1           // 1 = ON
// );
// // Aynı günler 22:00'de Röle1 KAPAT
// await client.SetTimerAsync(
//     index: 2,
//     time: new TimeSpan(22, 0, 0),
//     days: new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday },
//     output: 1,          // Röle 1
//     action: 0           // 0 = OFF
// );
//
// // Timer sistemini açık tut
// await client.EnableAllTimersAsync(true);

await client.SetTimerMultiAsync(
    days: new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday },
    onTimeHHmm:  "09:00",
    offTimeHHmm: "22:00",
    outputs: new[] { 1, 2 },
    strategy: MultiScheduleStrategy.Timers,
    startTimerIndex: 1   // Timer1..Timer4 kullanılır (2 röle x 2 timer)
);

await client.SetTimerMultiAsync(
    days: new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday }, // not: RuleBacklog'ta günleri dikkate almıyoruz
    onTimeHHmm:  "09:00",
    offTimeHHmm: "22:00",
    outputs: new[] { 1, 2, 3 },
    strategy: MultiScheduleStrategy.RuleBacklog,
    ruleIndex: 1
);
Console.WriteLine("Röle 1 açıldı.");

await app.RunAsync();