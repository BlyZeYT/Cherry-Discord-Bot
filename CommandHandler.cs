namespace Cherry;

using Cherry.Common;
using Discord;
using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

public class CommandHandler : DiscordClientService
{
    private readonly IServiceProvider _provider;
    private readonly DiscordSocketClient _client;
    private readonly CommandService _service;
    private readonly IConfiguration _config;
    private readonly LavaNode _lavaNode;
    private readonly ILogger _logger;
    private readonly IDatabase _database;
    private readonly IEmbedSender _embed;

    public CommandHandler(IServiceProvider provider, DiscordSocketClient client, CommandService service, 
        IConfiguration config, ILogger<DiscordClientService> logger, LavaNode lavaNode, IDatabase database, IEmbedSender embed) : base(client, logger)
    {
        _provider = provider;
        _client = client;
        _service = service;
        _config = config;
        _lavaNode = lavaNode;
        _logger = logger;
        _database = database;
        _embed = embed;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _client.Ready += SetGame;
        _client.Ready += ConnectLavalink;
        _client.JoinedGuild += OnJoinedGuild;
        _client.LeftGuild += OnLeftGuild;
        _client.MessageReceived += OnMessageReceived;
        _service.CommandExecuted += OnCommandExecuted;
        _client.ButtonExecuted += OnButtonExecuted;
        _client.SelectMenuExecuted += OnSelectedMenuOptions;

        _lavaNode.OnLog += arg =>
        {
            _logger.LogTrace(arg.Message, arg.Severity, arg.Exception);
            return Task.CompletedTask;
        };
        _lavaNode.OnStatsReceived += OnStatsReceived;
        _lavaNode.OnTrackStarted += OnTrackStart;
        _lavaNode.OnTrackEnded += OnTrackEnd;
        _lavaNode.OnTrackStuck += OnTrackStuck;
        _lavaNode.OnTrackException += OnTrackException;
        _lavaNode.OnWebSocketClosed += OnWebsocketClosed;

        await Client.WaitForReadyAsync(cancellationToken);
        await _service.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
    }

    private async Task SetGame() => await _client.SetGameAsync("BlyZeHD's Quality Content", null, ActivityType.Watching);

    private async Task ConnectLavalink()
    {
        if (!_lavaNode.IsConnected) await _lavaNode.ConnectAsync();
    }

    private async Task OnJoinedGuild(SocketGuild arg)
    {
        await _database.CreateGuildAsync(arg.Id);

        await _embed.JoinedGuildAsync(arg);
    }

    private async Task OnLeftGuild(SocketGuild arg)
    {
        await _database.RemoveGuildAsync(arg.Id);

        await _embed.LeftGuildAsync(arg);
    }

    private async Task OnStatsReceived(StatsEventArgs arg) => await Stats.UpdateAsync(arg.Cpu, arg.Frames, arg.Memory, arg.Uptime, arg.Players);

    private async Task OnTrackStart(TrackStartEventArgs arg)
    {
        if (await _database.GetRepeatAsync(arg.Player.VoiceChannel.GuildId))
        {
            await _embed.SendTrackRepeatedAsync(arg.Player.TextChannel, arg.Track);
            return;
        }

        await _embed.SendTrackStartedAsync(arg.Player.TextChannel, arg.Track);
    }

    private async Task OnTrackEnd(TrackEndedEventArgs arg)
    {
        switch (arg.Reason)
        {
            case TrackEndReason.Finished:

                if (await _database.GetRepeatAsync(arg.Player.VoiceChannel.GuildId))
                {
                    await arg.Player.PlayAsync(track =>
                    {
                        track.Track = arg.Track;
                        track.StartTime = TimeSpan.FromMilliseconds(1);
                    });
                    return;
                }

                if (!arg.Player.Queue.TryDequeue(out var queueable))
                {
                    await _embed.SendQueueCompletedAsync(arg.Player.TextChannel, arg.Track);
                    await _lavaNode.LeaveAsync(arg.Player.VoiceChannel);
                    return;
                }

                if (queueable is null)
                {
                    await _embed.SendTrackInvalidAsync(arg.Player.TextChannel);
                    return;
                }

                await arg.Player.PlayAsync(queueable);

                break;

            case TrackEndReason.Stopped:

                await _embed.SendTrackStoppedAsync(arg.Player.TextChannel, arg.Track);
                await _lavaNode.LeaveAsync(arg.Player.VoiceChannel);

                break;

            case TrackEndReason.LoadFailed or TrackEndReason.Cleanup:

                if (!arg.Player.Queue.TryDequeue(out var queueable1))
                {
                    await _embed.SendQueueCompletedAsync(arg.Player.TextChannel, arg.Track);
                    await _lavaNode.LeaveAsync(arg.Player.VoiceChannel);
                    return;
                }

                if (queueable1 is null)
                {
                    await _embed.SendTrackInvalidAsync(arg.Player.TextChannel);
                    return;
                }

                await arg.Player.PlayAsync(queueable1);

                break;
        }
    }

    private async Task OnTrackStuck(TrackStuckEventArgs arg)
    {
        await _database.SetRepeatAsync(arg.Player.VoiceChannel.GuildId, false);

        await _embed.SendTrackGotStuckAsync(arg.Player.TextChannel, arg.Track);

        _logger.LogLavaTrackStuckError(arg.Track, arg.Threshold);
    }

    private async Task OnTrackException(TrackExceptionEventArgs arg)
    {
        await _database.SetRepeatAsync(arg.Player.VoiceChannel.GuildId, false);

        await _embed.SendTrackThrewExceptionAsync(arg.Player.TextChannel, arg.Track, arg.Exception);

        _logger.LogLavaTrackExceptionError(arg.Track, arg.Exception);
    }

    private async Task OnWebsocketClosed(WebSocketClosedEventArgs arg)
    {
        await _database.SetRepeatAsync(arg.GuildId, false);

        if (_lavaNode.TryGetPlayer(_client.GetGuild(arg.GuildId), out var player))
            await _lavaNode.LeaveAsync(player.VoiceChannel);
    }
    
    private async Task OnMessageReceived(SocketMessage arg)
    {
        if (arg is not SocketUserMessage message) return;
        if (message.Source != MessageSource.User) return;

        var argPos = 0;

        if (message.Channel.IsDMChannel())
        {
            string dm = message.ToString();

            if (dm.StartsWith("idea:", StringComparison.OrdinalIgnoreCase)
                || dm.StartsWith("issue:", StringComparison.OrdinalIgnoreCase)
                || dm.StartsWith("bug:", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogFeedback(dm);
                await arg.Channel.SendMessageAsync("Message received!");
                return;
            }

            if (!message.HasStringPrefix(_config["prefix"], ref argPos) && !message.HasMentionPrefix(_client.CurrentUser, ref argPos)) return;
        }
        else
        {
            if (!message.HasStringPrefix(await _database.GetPrefixAsync(((SocketGuildUser)message.Author).Guild.Id)
                ?? _config["prefix"], ref argPos) && !message.HasMentionPrefix(_client.CurrentUser, ref argPos)) return;
        }

        var context = new SocketCommandContext(_client, message);
        await _service.ExecuteAsync(context, argPos, _provider);
    }

    private async Task OnCommandExecuted(Optional<CommandInfo> command, ICommandContext _, IResult result)
    {
        if (command.IsSpecified && !result.IsSuccess)
            await Task.Run(() => _logger.LogError($"Command Execution Error: {(result.Error.HasValue ? result.Error.Value : result)} -> {result.ErrorReason}"));
    }

    private async Task OnButtonExecuted(SocketMessageComponent arg)
    {
        if (arg.Data.CustomId == Cherry.INVITATION_DENIED_ID)
            await arg.Message.DeleteAsync();
    }

    private async Task OnSelectedMenuOptions(SocketMessageComponent arg)
    {
        if (arg.Data.CustomId is Cherry.CONTACTMENU_ID)
        {
            string selected = arg.Data.Values.First();

            await arg.Message.DeleteAsync();

            switch (selected)
            {
                case Cherry.CONTACTMENU_IDEA_ID:
                    await arg.RespondAsync("DM me **`Idea: <your message>`** to submit an idea!");
                    break;

                case Cherry.CONTACTMENU_ISSUE_ID:
                    await arg.RespondAsync("DM me **`Issue: <your message>`** to report an issue!");
                    break;

                case Cherry.CONTACTMENU_BUG_ID:
                    await arg.RespondAsync("DM me **`Bug: <your message>`** to report a bug!");
                    break;
            }
        }
    }
}