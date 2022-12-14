namespace Cherry.Common;

using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text;
using Victoria;

public class EmbedSender : IEmbedSender
{
    private readonly IConfiguration _config;
    private readonly IDatabase _database;
    private readonly ILogger<DiscordClientService> _logger;
    private readonly DiscordSocketClient _client;
    private readonly CommandService _service;

    public EmbedSender(IConfiguration config, IDatabase database, ILogger<DiscordClientService> logger, DiscordSocketClient client, CommandService service)
    {
        _config = config;
        _database = database;
        _logger = logger;
        _client = client;
        _service = service;
    }

    private Task<Embed> GetContactEmbed()
    {
        return Task.FromResult(new CherryEmbedBuilder()
            .WithAuthor(x =>
            {
                x.WithIconUrl(_client.CurrentUser.GetAvatarUrl(ImageFormat.Auto, 2048) ?? _client.CurrentUser.GetDefaultAvatarUrl());
                x.WithName($"{_client.CurrentUser.Username} - Contact");
            })
            .WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl(ImageFormat.Auto, 2048) ?? _client.CurrentUser.GetDefaultAvatarUrl())
            .WithDescription("Here you can contact my creator to submit ideas or report issues and bugs! \\😄").Build());
    }

    private Task<SelectMenuBuilder> GetContactMenu()
    {
        return Task.FromResult(new SelectMenuBuilder()
            .WithPlaceholder("Select an option")
            .WithCustomId(Cherry.CONTACTMENU_ID)
            .AddOption("Idea", Cherry.CONTACTMENU_IDEA_ID, "Submit an idea!", new Emoji("💡"))
            .AddOption("Issue", Cherry.CONTACTMENU_ISSUE_ID, "Report an issue!", new Emoji("⚠️"))
            .AddOption("Bug", Cherry.CONTACTMENU_BUG_ID, "Report a bug!", new Emoji("📛")));
    }

    private async Task SendWelcomeAsync(SocketGuild guild)
    {
        var embed = new CherryEmbedBuilder()
            .WithDescription($"My prefix is **{_config["prefix"]}**\n**{_config["prefix"]}help** to get all my commands!\n**{_config["prefix"]}prefix** to change my prefix!")
            .WithAuthor(author => author
            .WithIconUrl(guild.IconUrl)
            .WithName($"Hello {guild.Name}"))
            .Build();

        foreach (var channel in guild.TextChannels.Where(x => x.IsStandardTextChannel()).OrderBy(x => x.Position))
        {
            try
            {
                await channel.SendMessageAsync(embed: embed);
                break;
            }
            catch (Exception) { continue; }
        }
    }

    public async Task JoinedGuildAsync(SocketGuild guild)
    {
        await SendWelcomeAsync(guild);

        var cherry = Cherry.Server;

        if (cherry is null)
        {
            _logger.LogCherryServerError();
            return;
        }

        foreach (var channel in cherry.TextChannels.Where(x => x.IsStandardTextChannel()).OrderBy(x => x.Position))
        {
            try
            {
                await channel.SendMessageAsync(@"**Joined a new server** \🥳");
                await SendServerInfoAsync(channel, guild);
                break;
            }
            catch (Exception) { continue; }
        }
    }

    public async Task LeftGuildAsync(SocketGuild guild)
    {
        var cherry = _client.GetGuild(Cherry.GUILD_ID);

        if (cherry is null)
        {
            _logger.LogCherryServerError();
            return;
        }

        foreach (var channel in cherry.TextChannels.Where(x => x.IsStandardTextChannel()).OrderBy(x => x.Position))
        {
            try
            {
                await channel.SendMessageAsync(@"**Lefted a server** \🥲");
                await SendServerInfoAsync(channel, guild);
                break;
            }
            catch (Exception) { continue; }
        }
    }

    public async Task SendServerInfoAsync(IMessageChannel channel, SocketGuild guild)
    {
        var builder = new CherryEmbedBuilder()
            .WithTitle($"Information about {guild.Name}")
            .WithThumbnailUrl($"{guild.IconUrl}")
            .AddField("Created at", guild.CreatedAt.ToString("dd/MM/yyyy"))
            .AddField("Members", guild.MemberCount)
            .AddField("Prefix", await _database.GetPrefixAsync(guild.Id) ?? _config["prefix"])
            .AddField("Online", guild.Users.Count(x => x.Status is not UserStatus.Offline));

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task SendTrackStartedAsync(IMessageChannel channel, LavaTrack track)
    {
        var builder = new CherryEmbedBuilder()
                .WithTitle("Now playing \\🎶")
                .WithThumbnailUrl(await track.GetArtworkLinkAsync())
                .WithDescription(track.IsStream
                ? $"**{track.Title}**\nby {track.Author}, LIVE \\🔴\n[LINK]({track.Url}) \\🌐"
                : $"**{track.Title}**\nby {track.Author}, {track.GetFormattedDuration()}\n[LINK]({track.Url}) \\🌐");

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task SendTrackStoppedAsync(IMessageChannel channel, LavaTrack track)
    {
        var builder = new CherryEmbedBuilder()
                    .WithThumbnailUrl(await track.GetArtworkLinkAsync())
                    .WithTitle("**Stopped** \\❌")
                    .WithDescription($"**Last played:**\n[{track.Title}]({track.Url})");

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task SendQueueCompletedAsync(IMessageChannel channel, LavaTrack track)
    {
        var builder = new CherryEmbedBuilder()
                        .WithThumbnailUrl(await track.GetArtworkLinkAsync())
                        .WithTitle("**Queue completed** \\✅")
                        .WithDescription($"**Last played:**\n[{track.Title}]({track.Url})");

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task SendTrackRepeatedAsync(IMessageChannel channel, LavaTrack track) => await channel.SendMessageAsync($"**Repeated** {track.Title} 🔂");

    public async Task SendTrackGotStuckAsync(IMessageChannel channel, LavaTrack track) => await channel.SendMessageAsync($"Skipped \\⏭️:**{track.Title}** because it got stuck!");

    public async Task SendTrackThrewExceptionAsync(IMessageChannel channel, LavaTrack track, LavaException exception) => await channel.SendMessageAsync($"**Skipped \\⏭️:** {track.Title}\n**Reason \\⛔:** {exception.Message}");

    public async Task SendTrackInvalidAsync(IMessageChannel channel) => await channel.SendMessageAsync("Next item in queue is not a track. \\❌");

    public async Task SendTrackEnqueuedAsync(IMessageChannel channel, LavaTrack track)
    {
        var builder = new CherryEmbedBuilder()
                .WithTitle("Enqueued \\✅")
                .WithDescription($"**[{track.Title}]({track.Url})**");

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task SendTracksEnqueuedAsync(IMessageChannel channel, LavaTrack[] tracks)
    {
        if (tracks.Length is 1)
        {
            await SendTrackEnqueuedAsync(channel, tracks[0]);
        }
        else
        {
            var builder = new CherryEmbedBuilder()
                .WithTitle($"Enqueued {tracks.Length} tracks \\✅");

            await channel.SendMessageAsync(embed: builder.Build());
        }
    }

    public async Task SendTrackDequeuedAsync(IMessageChannel channel, LavaTrack track)
    {
        var builder = new CherryEmbedBuilder()
            .WithTitle("Dequeued \\🚫")
            .WithDescription($"**[{track.Title}]({track.Url})**");

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task SendTrackInfoAsync(IMessageChannel channel, LavaTrack track)
    {
        var artwork = await track.GetArtworkLinkAsync();
        var (source, sourceLink) = await track.GetSourceInfo();

        var builder = new CherryEmbedBuilder()
            .WithTitle("Track information  \\ℹ️")
            .WithThumbnailUrl(artwork);

        if (track.IsStream)
            builder.WithDescription($"**{track.Title}**\nby {track.Author}, LIVE \\🔴\n[{source}]{(sourceLink is "" ? "" : "({sourceLink})")} \\🔎\n[LINK]({track.Url}) \\🌐");
        else
            builder.WithDescription($"**{track.Title}**\nby {track.Author}, {track.GetFormattedDuration()}\n[{source}]{(sourceLink is "" ? "" : "({sourceLink})")} \\🔎\n[LINK]({track.Url}) \\🌐");

        await channel.SendMessageAsync(embed: builder.Build());

    }

    public async Task SendQueueAsync(IMessageChannel channel, LavaTrack[] fullQueue, int length)
    {
        var (firstTrack, remainedQueueEnumerable) = fullQueue.GetFirstAndRemainder();

        var sb = new StringBuilder($"**Currently playing:** [{firstTrack.Title}]({firstTrack.Url})\n");

        var remainedQueue = remainedQueueEnumerable.ToArray();
        
        if (remainedQueue.Length > 0)
        {
            if (remainedQueue.Length < length || length == -1) length = remainedQueue.Length;

            for (var i = 0; i < length; i++)
            {
                sb.AppendLine($"**{i + 1}:** [{remainedQueue[i].Title}]({remainedQueue[i].Url})");
            }
        }

        var builder = new CherryEmbedBuilder().WithDescription(sb.ToString());

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task SendLyricsAsync(IMessageChannel channel, LavaTrack track)
    {
        string lyrics = await track.GetLyrics();

        var builder = new CherryEmbedBuilder()
            .WithTitle("Lyrics \\📜")
            .WithDescription($"**{track.Title}**\n{(lyrics == "" ? "No lyrics found!" : lyrics)}")
            .WithThumbnailUrl(await track.GetArtworkLinkAsync());

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task SendRedditPostAsync(IMessageChannel channel, string imageUrl, string title, string url, string comments, string upvotes)
    {
        var builder = new CherryEmbedBuilder()
            .WithImageUrl(imageUrl)
            .WithTitle(title)
            .WithUrl($"https://reddit.com{url}")
            .WithFooter($"🗨 {comments} ⬆️ {upvotes}");

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task SendCoinflipAsync(IMessageChannel channel, bool head)
    {
        var builder = new CherryEmbedBuilder()
            .WithThumbnailUrl(head ? "https://i.imgur.com/tBQQSIZ.png" : "https://i.imgur.com/nEsC24S.png");

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task SendCherryInfoAsync(IMessageChannel channel, SocketGuild guild)
    {
        var builder = new CherryEmbedBuilder()
            .WithTitle(@"Information about me \🍒")
            .WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl(ImageFormat.Auto, 2048) ?? _client.CurrentUser.GetDefaultAvatarUrl())
            .AddField("ID", _client.CurrentUser.Id, true)
            .AddField("Created at", _client.CurrentUser.CreatedAt.ToString("dd.MM.yyyy"), true)
            .AddField("Creator", "BlyZe", true)
            .AddField("Servers", _client.Guilds.Count, true)
            .AddField("Joined at", guild.CurrentUser.JoinedAt.HasValue ? guild.CurrentUser.JoinedAt.Value.ToString("dd.MM.yyyy") : "", true)
            .AddField("** **", "** **", true)
            .AddField("Roles", string.Join(' ', guild.CurrentUser.Roles.Select(x => x.Mention)));

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task SendUserInfoAsync(IMessageChannel channel, SocketUser user)
    {
        var builder = new CherryEmbedBuilder()
            .WithThumbnailUrl(user.GetAvatarUrl(ImageFormat.Auto, 2048) ?? user.GetDefaultAvatarUrl())
            .WithTitle("Information about " + user.Username)
            .AddField("ID", user.Id)
            .AddField("Username", user.Username)
            .AddField("Created at", user.CreatedAt.ToString("dd.MM.yyyy"))
            .AddField("Joined at", ((SocketGuildUser)user).JoinedAt.HasValue ? ((SocketGuildUser)user).JoinedAt!.Value.ToString("dd.MM.yyyy") : "")
            .AddField("Roles", string.Join(' ', ((SocketGuildUser)user).Roles.Select(x => x.Mention)));

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task SendBotStatsAsync(IMessageChannel channel)
    {
        var builder = new CherryEmbedBuilder()
            .WithAuthor(x =>
            {
                x.WithIconUrl(_client.CurrentUser.GetAvatarUrl(ImageFormat.Auto, 2048) ?? _client.CurrentUser.GetDefaultAvatarUrl());
                x.WithName($"{_client.CurrentUser.Username} - Bot Statistics");
            })
            .WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl(ImageFormat.Auto, 2048) ?? _client.CurrentUser.GetDefaultAvatarUrl())
            .WithDescription($"**General**\n```diff\n- Playing Players :: {Stats.Players}\n" +
                $"- Uptime :: {Stats.Uptime.Days} days and {Stats.Uptime.Hours} hours\n" +
                $"- Latency :: {_client.Latency}ms\n```**System**\n```diff\n" +
                $"- OS :: {RuntimeInformation.OSDescription[..RuntimeInformation.OSDescription.IndexOf('+')]}\n" +
                $"- CPU :: Broadcom BCM2711\n- Cores :: 4\n- Speed :: 1,5 GHz\n" +
                $"- CPU Usage :: {Math.Round(Stats.CPU.SystemLoad, 2, MidpointRounding.ToEven)} %\n" +
                $"- RAM :: 8 GB LPDDR4-3200 SDRAM\n- RAM Speed :: 3200 MHz\n" +
                $"- RAM Usage :: {Stats.Memory.Allocated * 0.00000095367431640625} MB\n```");

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task SendNewsAsync(IMessageChannel channel, string title, string message)
    {
        var builder = new CherryEmbedBuilder()
            .WithTitle(title)
            .WithDescription(message);

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task SendBanlistAsync(IMessageChannel channel, IEnumerable<RestBan> banlist)
    {
        var sb = new StringBuilder();

        foreach (var ban in banlist)
        {
            sb.AppendLine($"**{ban.User.Username} #{ban.User.Discriminator}**");
            sb.AppendLine($"Reason: {ban.Reason} - ID: {ban.User.Id}");
        }

        if (!banlist.Any())
        {
            await channel.SendMessageAsync("The banlist is empty");
            return;
        }

        var builder = new CherryEmbedBuilder()
            .WithTitle("Banlist \\⛔")
            .WithDescription(sb.ToString());

        await channel.SendMessageAsync(embed: builder.Build());
    }

    public async Task<bool> TrySendContactDMAsync(SocketUser user)
    {
        var component = new ComponentBuilder()
            .WithSelectMenu(await GetContactMenu())
            .Build();

        try
        {
            await user.SendMessageAsync(embed: await GetContactEmbed(), components: component);
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    public async Task<bool> TrySendHelpDMAsync(SocketUser user, string specificInfo)
    {
        string prefix = _config["prefix"]!;

        string avatar = _client.CurrentUser.GetAvatarUrl(size: 2048) ?? _client.CurrentUser.GetDefaultAvatarUrl();

        var builder = new CherryEmbedBuilder()
            .WithThumbnailUrl(avatar);

        if (string.IsNullOrWhiteSpace(specificInfo))
        {
            builder.WithAuthor(x =>
            {
                x.WithIconUrl(avatar);
                x.WithName($"{_client.CurrentUser.Username} - Command List");
            }).WithFooter(x =>
            {
                x.WithIconUrl(Cherry.INFORMATION);
                x.WithText($"{prefix}help <command> to get more information about a specific command");
            });

            var sb = new StringBuilder();

            foreach (var module in _service.Modules.Where(x => x.Name is not "Owner"))
            {
                foreach (var command in module.Commands)
                {
                    sb.Append($"`{command.Name}` ");
                }

                sb.Length--;
                builder.AddField($"▫️**{module.Name} [{module.Commands.Count}]**\n", $"{sb}");
                sb.Length = 0;
            }
        }
        else
        {
            var command = _service.Commands.FirstOrDefault(x => x.Name == specificInfo);

            if (command is null)
            {
                try
                {
                    await user.SendMessageAsync("This command does not exist");
                }
                catch (Exception)
                {
                    return false;
                }

                return true;
            }

            builder.WithAuthor(x =>
            {
                x.WithIconUrl(avatar);
                x.WithName($"{_client.CurrentUser.Username} - Information about `{command.Name}` command");
            }).WithFooter(x =>
            {
                x.WithIconUrl(Cherry.INFORMATION);
                x.WithText("<> = required | [] = optional");
            })
            .AddField("Name", $"`{command.Name}`")
            .AddField("Description", command.Summary ?? "No description provided");

            var sb = new StringBuilder();

            foreach (var alias in command.Aliases)
            {
                sb.Append($"`{alias}` ");
            }

            sb.Length--;
            builder.AddField("Aliases", $"{sb}");
            sb.Length = 0;

            builder.AddField("Usage", $"**```\n{prefix}{command.Remarks}\n```**");
        }

        try
        {
            await user.SendMessageAsync(embed: builder.Build());
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    public async Task<bool> TrySendInvitationDMAsync(SocketUser user, IUser userToInvite, SocketGuild guild, IInviteMetadata invitation)
    {
        var builder = new CherryEmbedBuilder()
                .WithTitle($"**{user.Username}** invites you to **{guild.Name}**")
                .WithDescription($"**Members:** {guild.MemberCount}\n**Created at:** {guild.CreatedAt:dd/MM/yyyy}")
                .WithThumbnailUrl($"{guild.IconUrl}");

        var button = new ComponentBuilder()
            .WithButton("Join server!", null, ButtonStyle.Link, null, invitation.Url)
            .WithButton("Delete invitation!", Cherry.INVITATION_DENIED_ID, ButtonStyle.Danger);

        try
        {
            await userToInvite.SendMessageAsync(embed: builder.Build(), components: button.Build());
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }
}

public interface IEmbedSender
{
    public Task JoinedGuildAsync(SocketGuild guild);

    public Task LeftGuildAsync(SocketGuild guild);

    public Task SendServerInfoAsync(IMessageChannel channel, SocketGuild guild);

    public Task SendTrackStartedAsync(IMessageChannel channel, LavaTrack track);

    public Task SendTrackStoppedAsync(IMessageChannel channel, LavaTrack track);

    public Task SendQueueCompletedAsync(IMessageChannel channel, LavaTrack track);

    public Task SendTrackRepeatedAsync(IMessageChannel channel, LavaTrack track);

    public Task SendTrackGotStuckAsync(IMessageChannel channel, LavaTrack track);

    public Task SendTrackThrewExceptionAsync(IMessageChannel channel, LavaTrack track, LavaException exception);

    public Task SendTrackInvalidAsync(IMessageChannel channel);

    public Task SendTrackEnqueuedAsync(IMessageChannel channel, LavaTrack track);

    public Task SendTracksEnqueuedAsync(IMessageChannel channel, LavaTrack[] tracks);

    public Task SendTrackDequeuedAsync(IMessageChannel channel, LavaTrack track);

    public Task SendTrackInfoAsync(IMessageChannel channel, LavaTrack track);

    public Task SendQueueAsync(IMessageChannel channel, LavaTrack[] fullQueue, int length);

    public Task SendLyricsAsync(IMessageChannel channel, LavaTrack track);

    public Task SendRedditPostAsync(IMessageChannel channel, string imageUrl, string title, string url, string comments, string upvotes);

    public Task SendCoinflipAsync(IMessageChannel channel, bool head);

    public Task SendCherryInfoAsync(IMessageChannel channel, SocketGuild guild);

    public Task SendUserInfoAsync(IMessageChannel channel, SocketUser user);

    public Task SendBotStatsAsync(IMessageChannel channel);

    public Task SendNewsAsync(IMessageChannel channel, string title, string message);

    public Task SendBanlistAsync(IMessageChannel channel, IEnumerable<RestBan> banlist);

    public Task<bool> TrySendContactDMAsync(SocketUser user);

    public Task<bool> TrySendHelpDMAsync(SocketUser user, string specificInfo);

    public Task<bool> TrySendInvitationDMAsync(SocketUser user, IUser userToInvite, SocketGuild guild, IInviteMetadata invitation);
}

public class CherryEmbedBuilder : EmbedBuilder
{
    public CherryEmbedBuilder() => WithColor(new Color(217, 45, 67));
}