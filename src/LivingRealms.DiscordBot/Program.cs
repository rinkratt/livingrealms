using Discord;
using Discord.WebSocket;
using LivingRealms.DiscordBot;
using Serilog;
using Serilog.Formatting.Json;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "LivingRealms.DiscordBot")
    .WriteTo.Console(new JsonFormatter()));

builder.Services.Configure<DiscordBotOptions>(builder.Configuration.GetSection(DiscordBotOptions.SectionName));
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection(OpenAiOptions.SectionName));

builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds,
    LogGatewayIntentWarnings = true
}));

builder.Services.AddHttpClient<OpenAiResponsesClient>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
    client.Timeout = TimeSpan.FromSeconds(45);
});

builder.Services.AddHostedService<DiscordBotWorker>();

await builder.Build().RunAsync();
