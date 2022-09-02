namespace Cherry.Modules;

using Cherry.Common;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

[Name("Owner")]
[RequireOwner]
public class OwnerModule : CherryModuleBase
{
    private readonly DiscordSocketClient _client;
    private readonly IDatabase _database;
    private readonly IEmbedSender _embed;

    public OwnerModule(DiscordSocketClient client, IDatabase database, IEmbedSender embed)
    {
        _client = client;
        _database = database;
        _embed = embed;
    }

    [Command("getserver", RunMode = RunMode.Async)]
    [Remarks("getserver [guildId]")]
    [RequireOwner]
    public async Task GetServer([Remainder] string guildIdStr = "")
    {
        if (string.IsNullOrWhiteSpace(guildIdStr))
        {
            var guilds = _client.Guilds;

            foreach (var guild in guilds)
            {
                if (guild is not null)
                    await _embed.SendServerInfoAsync((ITextChannel)Context.Channel, guild);
            }
        }
        else
        {
            if (!ulong.TryParse(guildIdStr, out var guildId))
            {
                await ReplyAsync("Please enter a valid number");
                return;
            }

            var guild = _client.GetGuild(guildId);

            if (guild is null)
            {
                await ReplyAsync("This is not a valid guild id");
                return;
            }

            await _embed.SendServerInfoAsync((ITextChannel)Context.Channel, guild);
        }
    }

    [Command("sendtoallservers", RunMode = RunMode.Async)]
    [Remarks("sendtoallservers <title|message>")]
    [RequireOwner]
    public async Task SendToAllServers([Remainder] string fullText = "")
    {
        if (string.IsNullOrWhiteSpace(fullText))
        {
            await ReplyAsync("I can't send an empty message to other servers");
            return;
        }

        string[] splitted = fullText.Split('|');

        if (splitted.Length is not 2)
        {
            await ReplyAsync("The message is not correctly formatted");
            return;
        }

        foreach (var guild in _client.Guilds)
        {
            if (guild is not null)
            {
                await _embed.SendNewsAsync(guild.DefaultChannel, splitted[0], splitted[1]);
            }
        }
    }

    [Command("checkdatabaseconnection", RunMode = RunMode.Async)]
    [Remarks("checkdatabaseconnection")]
    [RequireOwner]
    public async Task CheckDatabaseConnection()
        => await ReplyAsync($"The connection was established in **{await _database.CheckConnectionAsync()} ms**");
}