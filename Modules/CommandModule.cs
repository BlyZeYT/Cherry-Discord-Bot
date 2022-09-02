namespace Cherry.Modules;

using Cherry.Common;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;

[Name("General")]
public class CommandModule : CherryModuleBase
{
    private readonly IEmbedSender _embed;

    public CommandModule(IEmbedSender embed)
    {
        _embed = embed;
    }

    [Command("help", RunMode = RunMode.Async)]
    [Summary("Shows the command list or specific information about a command")]
    [Remarks("help [command]")]
    [RequireContext(ContextType.Guild)]
    [RequireContext(ContextType.DM)]
    public async Task Help([Remainder] string specificInfo = "")
    {
        var couldSend = await _embed.TrySendHelpDMAsync(Context.User, specificInfo);

        if (!Context.Channel.IsDMChannel())
        {
            if (couldSend) await ReplyAsync("I sent you a DM!");
            else await ReplyAsync("I can't send a DM to you!");
        }
    }

    [Command("contact", RunMode = RunMode.Async)]
    [Summary("Contact my creator to submit ideas or report issues and bugs")]
    [Remarks("contact")]
    [RequireContext(ContextType.Guild)]
    [RequireContext(ContextType.DM)]
    public async Task Contact()
    {
        var couldSend = await _embed.TrySendContactDMAsync(Context.User);

        if (!Context.Channel.IsDMChannel())
        {
            if (couldSend) await ReplyAsync("I sent you a DM!");
            else await ReplyAsync("I can't send a DM to you!");
        }
    }

    [Command("stats", RunMode = RunMode.Async)]
    [Summary("Get the stats of the bot")]
    [Remarks("stats")]
    [RequireContext(ContextType.Guild)]
    [RequireContext(ContextType.DM)]
    public async Task Stats() => await _embed.SendBotStatsAsync((ITextChannel)Context.Channel);

    [Command("info", RunMode = RunMode.Async)]
    [Alias("userinfo")]
    [Summary("Get information about you or any other user")]
    [Remarks("info [@user]")]
    [RequireContext(ContextType.Guild)]
    public async Task UserInfo([Remainder] SocketGuildUser? user = null) => await _embed.SendUserInfoAsync((ITextChannel)Context.Channel, user ?? Context.User);

    [Command("server", RunMode = RunMode.Async)]
    [Alias("guild")]
    [Summary("Get information about the server")]
    [Remarks("server")]
    [RequireContext(ContextType.Guild)]
    public async Task ServerInfo() => await _embed.SendServerInfoAsync((ITextChannel)Context.Channel, Context.Guild);

    [Command("bot", RunMode = RunMode.Async)]
    [Alias("botinfo")]
    [Summary("Get information about the bot")]
    [Remarks("bot")]
    [RequireContext(ContextType.Guild)]
    [RequireContext(ContextType.DM)]
    public async Task BotInfo() => await _embed.SendCherryInfoAsync((ITextChannel)Context.Channel, Context.Guild);

    [Command("reddit", RunMode = RunMode.Async)]
    [Alias("meme")]
    [Summary("Get a random reddit meme or choose a subreddit")]
    [Remarks("reddit [subreddit]")]
    [RequireContext(ContextType.Guild)]
    [RequireContext(ContextType.DM)]
    public async Task Reddit([Remainder] string subreddit = "")
    {
        using (var client = new HttpClient())
        {
            string result;
            try
            {
                result = await client.GetStringAsync($"https://reddit.com/r/{(string.IsNullOrWhiteSpace(subreddit) ? "memes" : subreddit)}/random.json?limit=1");
            }
            catch (Exception)
            {
                await ReplyAsync("Couldn't find anything :(");
                return;
            }

            if (!result.StartsWith('['))
            {
                await ReplyAsync("This subreddit does not exist!");
                return;
            }

            var array = JArray.Parse(result);
            var post = JObject.Parse(array[0]["data"]!["children"]![0]!["data"]!.ToString());

            if (post["over_18"]?.ToString() is "True" && !((ITextChannel)Context.Channel).IsNsfw)
            {
                await ReplyAsync("The subreddit contains NSFW content, while this is a SFW channel!");
                return;
            }

            await _embed.SendRedditPostAsync((ITextChannel)Context.Channel, $"{post["url"]}", $"{post["title"]}", $"{post["permalink"]}", $"{post["num_comments"]}", $"{post["ups"]}");
        }
    }

    [Command("coinflip", RunMode = RunMode.Async)]
    [Summary("Flip a coin")]
    [Remarks("coinflip")]
    [RequireContext(ContextType.Guild)]
    [RequireContext(ContextType.DM)]
    public async Task Coinflip() => await _embed.SendCoinflipAsync((ITextChannel)Context.Channel, new Random().Next(0, 2) == 1);
}