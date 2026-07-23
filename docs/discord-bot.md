# Living Realms Discord AI Bot

The bot adds a guild-scoped `/ask` command to the Living Realms Discord server.
It uses Discord application commands, so it does not need the privileged Message
Content intent and does not read ordinary channel conversations.

## Required secrets

- A Discord bot token from the Living Realms AI Bot application.
- An OpenAI API key from the OpenAI platform project used for the bot.

Never commit either secret. For local development, store them with .NET user
secrets from the repository root:

```powershell
dotnet user-secrets set "DiscordBot:Token" "YOUR_DISCORD_BOT_TOKEN" `
  --project src/LivingRealms.DiscordBot/LivingRealms.DiscordBot.csproj

dotnet user-secrets set "OpenAI:ApiKey" "YOUR_OPENAI_API_KEY" `
  --project src/LivingRealms.DiscordBot/LivingRealms.DiscordBot.csproj
```

The Living Realms guild ID is already configured. Production should provide the
same values through protected environment variables:

```text
DiscordBot__Token
DiscordBot__GuildId
OpenAI__ApiKey
OpenAI__Model
```

## Discord application settings

1. Create a Discord application named `Living Realms AI Bot`.
2. Use a Guild Install context.
3. Add the `applications.commands` and `bot` scopes.
4. Grant only `View Channels`, `Send Messages`, and `Read Message History`.
5. Install the app into the Living Realms server.
6. Do not enable the privileged Message Content intent; `/ask` does not need it.

The worker registers `/ask` as a guild command when it connects. Guild commands
appear immediately, which keeps development and testing predictable.

## Run locally

```powershell
dotnet run --project src/LivingRealms.DiscordBot/LivingRealms.DiscordBot.csproj
```

In Discord, run:

```text
/ask question: What is Stonehaven Valley?
```

## Operational safeguards

- Responses are capped below Discord's 2,000-character message limit.
- User mentions produced by the model cannot ping members or roles.
- Each user has a configurable cooldown to control API cost and spam.
- At most two OpenAI requests run concurrently in one bot process.
- OpenAI response storage is disabled for bot requests.
- User questions and API response bodies are not written to application logs.

The bot process must remain running to answer commands. Deploy it as a supervised
service on the existing server rather than as an intermittent scheduled task.
