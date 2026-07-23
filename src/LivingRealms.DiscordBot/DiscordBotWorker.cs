using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace LivingRealms.DiscordBot;

public sealed partial class DiscordBotWorker(
    DiscordSocketClient client,
    OpenAiResponsesClient openAi,
    IOptions<DiscordBotOptions> options,
    ILogger<DiscordBotWorker> logger) : BackgroundService
{
    private const string AskCommandName = "ask";
    private readonly DiscordBotOptions _options = options.Value;
    private readonly ConcurrentDictionary<ulong, DateTimeOffset> _nextAllowedAt = new();
    private readonly SemaphoreSlim _requestSlots = new(2, 2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateConfiguration();

        client.Log += HandleDiscordLogAsync;
        client.Ready += HandleReadyAsync;
        client.SlashCommandExecuted += HandleSlashCommandAsync;

        try
        {
            await client.LoginAsync(TokenType.Bot, _options.Token);
            await client.StartAsync();
            LogBotStarted(logger, _options.GuildId);
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        finally
        {
            await client.StopAsync();
            await client.LogoutAsync();
            client.Log -= HandleDiscordLogAsync;
            client.Ready -= HandleReadyAsync;
            client.SlashCommandExecuted -= HandleSlashCommandAsync;
        }
    }

    private async Task HandleReadyAsync()
    {
        var guild = client.GetGuild(_options.GuildId)
            ?? throw new InvalidOperationException($"The bot is not installed in Discord guild {_options.GuildId}.");

        var command = new SlashCommandBuilder()
            .WithName(AskCommandName)
            .WithDescription("Ask the Living Realms Guide about the game or current play test")
            .AddOption(
                "question",
                ApplicationCommandOptionType.String,
                "What would you like to know?",
                isRequired: true);

        await guild.CreateApplicationCommandAsync(command.Build());
        LogCommandRegistered(logger, guild.Id);
    }

    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        if (command.GuildId != _options.GuildId || command.Data.Name != AskCommandName)
        {
            return;
        }

        var question = command.Data.Options.FirstOrDefault(option => option.Name == "question")?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(question))
        {
            await command.RespondAsync("Please include a question for the Living Realms Guide.", ephemeral: true);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (_nextAllowedAt.TryGetValue(command.User.Id, out var nextAllowedAt) && nextAllowedAt > now)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling((nextAllowedAt - now).TotalSeconds));
            await command.RespondAsync($"Please wait {seconds} seconds before asking again.", ephemeral: true);
            return;
        }

        if (!await _requestSlots.WaitAsync(0))
        {
            await command.RespondAsync("The guide is helping other players right now. Please try again shortly.", ephemeral: true);
            return;
        }

        _nextAllowedAt[command.User.Id] = now.AddSeconds(_options.CooldownSeconds);
        await command.DeferAsync();

        try
        {
            var answer = await openAi.AskAsync(question, CancellationToken.None);
            await command.ModifyOriginalResponseAsync(properties =>
            {
                properties.Content = DiscordMessageFormatter.Fit(answer);
                properties.AllowedMentions = AllowedMentions.None;
            });
        }
        catch (Exception exception)
        {
            LogCommandFailure(logger, exception, command.User.Id);
            await command.ModifyOriginalResponseAsync(properties =>
            {
                properties.Content = "The Living Realms Guide could not answer just now. Please try again in a moment.";
                properties.AllowedMentions = AllowedMentions.None;
            });
        }
        finally
        {
            _requestSlots.Release();
        }
    }

    private Task HandleDiscordLogAsync(LogMessage message)
    {
        LogDiscordMessage(logger, message.Severity, message.Source, message.Message ?? message.Exception?.Message ?? "No message");
        return Task.CompletedTask;
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            throw new InvalidOperationException("DiscordBot:Token is required.");
        }

        if (_options.GuildId == 0)
        {
            throw new InvalidOperationException("DiscordBot:GuildId is required.");
        }

        if (_options.CooldownSeconds is < 0 or > 300)
        {
            throw new InvalidOperationException("DiscordBot:CooldownSeconds must be between 0 and 300.");
        }
    }

    [LoggerMessage(EventId = 2100, Level = LogLevel.Information, Message = "Discord bot started for guild {GuildId}")]
    private static partial void LogBotStarted(ILogger logger, ulong guildId);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Information, Message = "Registered /ask in guild {GuildId}")]
    private static partial void LogCommandRegistered(ILogger logger, ulong guildId);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Error, Message = "Discord /ask failed for user {UserId}")]
    private static partial void LogCommandFailure(ILogger logger, Exception exception, ulong userId);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Information, Message = "Discord {Severity} from {Source}: {DiscordMessage}")]
    private static partial void LogDiscordMessage(ILogger logger, LogSeverity severity, string source, string discordMessage);
}
