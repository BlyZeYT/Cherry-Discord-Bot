namespace Cherry.Modules;

using Cherry.Common;
using Discord;
using Discord.Commands;
using Victoria;
using Victoria.Enums;

[Name("Music")]
public class MusicModule : CherryModuleBase
{
    private readonly LavaNode _lavaNode;
    private readonly IMusicHelper _music;
    private readonly IEmbedSender _embed;
    private readonly IDatabase _database;

    public MusicModule(LavaNode lavaNode, IMusicHelper music, IEmbedSender embed, IDatabase database)
    {
        _lavaNode = lavaNode;
        _music = music;
        _embed = embed;
        _database = database;
    }

    [Command("play", RunMode = RunMode.Async)]
    [Alias("p")]
    [Summary("Plays videos, playlists or streams from YouTube and Twitch")]
    [Remarks("play <search/link>")]
    [RequireContext(ContextType.Guild)]
    public async Task Play([Remainder] string searchQuery = "")
        => await _music.PlayMusicAsync((ITextChannel)Context.Channel,
            (IVoiceState)Context.User, Context.Guild, searchQuery, Source.YouTube);

    [Command("ytmusic", RunMode = RunMode.Async)]
    [Alias("youtubemusic", "ytm")]
    [Summary("Plays tracks from Youtube Music")]
    [Remarks("ytmusic <search/link>")]
    [RequireContext(ContextType.Guild)]
    public async Task YouTubeMusic([Remainder] string searchQuery = "")
        => await _music.PlayMusicAsync((ITextChannel)Context.Channel,
            (IVoiceState)Context.User, Context.Guild, searchQuery, Source.YouTubeMusic);

    [Command("soundcloud", RunMode = RunMode.Async)]
    [Alias("sc")]
    [Summary("Plays tracks from Soundcloud")]
    [Remarks("soundcloud <search/link>")]
    [RequireContext(ContextType.Guild)]
    public async Task Soundcloud([Remainder] string searchQuery = "")
        => await _music.PlayMusicAsync((ITextChannel)Context.Channel,
            (IVoiceState)Context.User, Context.Guild, searchQuery, Source.Soundcloud);

    [Command("pause", RunMode = RunMode.Async)]
    [Summary("Pauses what is currently playing")]
    [Remarks("pause")]
    [RequireContext(ContextType.Guild)]
    public async Task Pause()
    {
        var voiceState = (IVoiceState)Context.User;

        if (voiceState?.VoiceChannel is null)
        {
            await ReplyAsync("Where are you?");
            return;
        }

        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.VoiceChannel != voiceState.VoiceChannel)
        {
            await ReplyAsync("I'm currently in another channel");
            return;
        }

        switch (player.PlayerState)
        {
            case PlayerState.Playing:
                await player.PauseAsync();
                await ReplyAsync("**Paused** ⏸️");
                break;

            case PlayerState.Paused:
                await ReplyAsync("I'm already pausing music");
                break;

            default:
                await ReplyAsync("Nothing is playing!");
                break;
        }
    }

    [Command("resume", RunMode = RunMode.Async)]
    [Summary("Resumes what is currently paused")]
    [Remarks("resume")]
    [RequireContext(ContextType.Guild)]
    public async Task Resume()
    {
        var voiceState = (IVoiceState)Context.User;

        if (voiceState?.VoiceChannel is null)
        {
            await ReplyAsync("Where are you?");
            return;
        }

        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.VoiceChannel != voiceState.VoiceChannel)
        {
            await ReplyAsync("I'm currently in another channel");
            return;
        }

        switch (player.PlayerState)
        {
            case PlayerState.Playing:
                await ReplyAsync("I'm already playing music");
                break;

            case PlayerState.Paused:
                await player.ResumeAsync();
                await ReplyAsync("**Resumed** ▶️");
                break;

            default:
                await ReplyAsync("Nothing is playing!");
                break;
        }
    }

    [Command("skip", RunMode = RunMode.Async)]
    [Alias("s")]
    [Summary("Skips whats currently playing")]
    [Remarks("skip [queue number]")]
    [RequireContext(ContextType.Guild)]
    public async Task Skip([Remainder] string trackNumber = "")
    {
        var voiceState = (IVoiceState)Context.User;

        if (voiceState?.VoiceChannel is null)
        {
            await ReplyAsync("Where are you?");
            return;
        }

        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.VoiceChannel != voiceState.VoiceChannel)
        {
            await ReplyAsync("I'm currently in another channel");
            return;
        }

        if (player.PlayerState is PlayerState.Playing or PlayerState.Paused)
        {
            await _database.SetRepeatAsync(player.VoiceChannel.GuildId, false);

            if (string.IsNullOrWhiteSpace(trackNumber))
            {
                if (player.Queue.IsEmpty())
                {
                    await player.StopAsync();
                    return;
                }

                await player.SkipAsync();
                await ReplyAsync("**Skipped** ⏭️");

                return;
            }

            if (!int.TryParse(trackNumber, out var queueNumber))
            {
                await ReplyAsync("Please enter a valid number");
                return;
            }

            if (player.Queue.Count < queueNumber)
            {
                await ReplyAsync("There is no track at this Queue number");
                return;
            }

            if (!player.Queue.TryRemoveAt(queueNumber - 1, out LavaTrack removedTrack))
            {
                await ReplyAsync("Can't remove the track at number " + queueNumber);
                return;
            }

            await ReplyAsync($"Dequeued [{removedTrack.Title}]({removedTrack.Url})");
        }
        else await ReplyAsync("I'm not playing any music to skip");
    }

    [Command("stop", RunMode = RunMode.Async)]
    [Summary("Stops the whole queue and the bot leaves")]
    [Remarks("stop")]
    [RequireContext(ContextType.Guild)]
    public async Task Stop()
    {
        var voiceState = (IVoiceState)Context.User;

        if (voiceState?.VoiceChannel is null)
        {
            await ReplyAsync("Where are you?");
            return;
        }

        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.VoiceChannel != voiceState.VoiceChannel)
        {
            await ReplyAsync("I'm currently in another channel");
            return;
        }

        if (player.PlayerState is PlayerState.Playing or PlayerState.Paused)
        {
            await _database.SetRepeatAsync(player.VoiceChannel.GuildId, false);
            await player.StopAsync();
            return;
        }

        await ReplyAsync("I'm not playing anything");
    }

    [Command("trackinfo", RunMode = RunMode.Async)]
    [Alias("track")]
    [Summary("Get info about any track in the queue")]
    [Remarks("trackinfo [position in queue]")]
    [RequireContext(ContextType.Guild)]
    public async Task TrackInfo([Remainder] string queuePos = "")
    {
        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.PlayerState is not PlayerState.Playing or PlayerState.Paused)
        {
            await ReplyAsync("I'm not playing anything");
            return;
        }

        LavaTrack track;

        if (string.IsNullOrWhiteSpace(queuePos))
        {
            track = player.Track;
        }
        else
        {
            if (!int.TryParse(queuePos, out var queueNumber))
            {
                await ReplyAsync("Please enter a valid number");
                return;
            }

            if (player.Queue.Count < queueNumber)
            {
                await ReplyAsync("There is no track at this Queue number");
                return;
            }

            try
            {
                track = player.Queue.ElementAt(queueNumber - 1);
            }
            catch (Exception)
            {
                await ReplyAsync("Can't get the track at number " + queueNumber);
                return;
            }
        }

        await _embed.SendTrackInfoAsync((ITextChannel)Context.Channel, track);
    }

    [Command("queue", RunMode = RunMode.Async)]
    [Summary("Shows the tracks in queue")]
    [Remarks("queue <queue length>")]
    [RequireContext(ContextType.Guild)]
    public async Task Queue([Remainder] string queuePos = "")
    {
        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.PlayerState is not PlayerState.Playing or PlayerState.Paused)
        {
            await ReplyAsync("The Queue is currently empty");
            return;
        }

        if (!int.TryParse(queuePos, out var queueNumber) || queueNumber < 1)
        {
            await ReplyAsync("Please enter a valid number");
            return;
        }

        LavaTrack[] fullQueue = player.Queue.Prepend(player.Track).ToArray();

        await _embed.SendQueueAsync((ITextChannel)Context.Channel, fullQueue, queueNumber);
    }

    [Command("shuffle", RunMode = RunMode.Async)]
    [Summary("Shuffles the whole queue")]
    [Remarks("shuffle")]
    [RequireContext(ContextType.Guild)]
    public async Task Shuffle()
    {
        var voiceState = (IVoiceState)Context.User;

        if (voiceState?.VoiceChannel is null)
        {
            await ReplyAsync("Where are you?");
            return;
        }

        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.VoiceChannel != voiceState.VoiceChannel)
        {
            await ReplyAsync("I'm currently in another channel");
            return;
        }

        if (player.PlayerState is not PlayerState.Playing or PlayerState.Paused)
        {
            await ReplyAsync("I'm not playing anything");
            return;
        }

        if (player.Queue.Count <= 1)
        {
            await ReplyAsync("I have nothing to shuffle");
            return;
        }

        player.Queue.Shuffle();
        await ReplyAsync("**Shuffled** 🔀");
    }

    [Command("repeat", RunMode = RunMode.Async)]
    [Alias("loop")]
    [Summary("Repeats whats currently playing until its disabled")]
    [Remarks("repeat")]
    [RequireContext(ContextType.Guild)]
    public async Task Repeat()
    {
        var voiceState = (IVoiceState)Context.User;

        if (voiceState?.VoiceChannel is null)
        {
            await ReplyAsync("Where are you?");
            return;
        }

        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.VoiceChannel != voiceState.VoiceChannel)
        {
            await ReplyAsync("I'm currently in another channel");
            return;
        }

        if (player.PlayerState is not PlayerState.Playing or PlayerState.Paused)
        {
            await ReplyAsync("I'm not playing anything");
            return;
        }

        if (player.Track.IsStream)
        {
            await ReplyAsync("I can't repeat a livestream");
            return;
        }

        if (await _database.GetRepeatAsync(player.VoiceChannel.GuildId))
            await ReplyAsync($"{(await _database.SetRepeatAsync(Context.Guild.Id, false) ? "**Repeat off** \\❌" : "Couldn't set Repeat. Please try again later")}");
        else
            await ReplyAsync($"{(await _database.SetRepeatAsync(Context.Guild.Id, true) ? "**Repeat on** \\✅" : "Couldn't set Repeat. Please try again later")}");
    }

    [Command("volume", RunMode = RunMode.Async)]
    [Alias("v")]
    [Summary("Get the bots volume level or change it to a value between 0 and 200")]
    [Remarks("volume [amount (0 - 200)]")]
    [RequireContext(ContextType.Guild)]
    public async Task Volume([Remainder] string volumeStr = "")
    {
        var voiceState = (IVoiceState)Context.User;

        if (voiceState?.VoiceChannel is null)
        {
            await ReplyAsync("Where are you?");
            return;
        }

        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.VoiceChannel != voiceState.VoiceChannel)
        {
            await ReplyAsync("I'm currently in another channel");
            return;
        }

        if (player.PlayerState is not PlayerState.Playing or PlayerState.Paused)
        {
            await ReplyAsync("I'm not playing anything");
            return;
        }

        if (string.IsNullOrWhiteSpace(volumeStr))
        {
            switch (((double)player.Volume) * 100)
            {
                case 0:
                    await ReplyAsync("I'm currently **muted** \\🔇");
                    break;

                case 500:
                    await ReplyAsync("I'm currently on **EARRAPE** \\🤯");
                    break;

                default:
                    await ReplyAsync($"I'm currently on **{player.Volume}** {(player.Volume >= (int)(Cherry.STANDARD_VOLUME * 100) ? "\\🔊" : "\\🔉")}");
                    break;
            }

            return;
        }

        if (volumeStr.Equals("earrape", StringComparison.OrdinalIgnoreCase)) volumeStr = "500";

        if (!int.TryParse(volumeStr, out var volumeVal))
        {
            await ReplyAsync("Please enter a valid number");
            return;
        }

        var volume = ((double)volumeVal) / 100;

        if (volume is < Cherry.MIN_VOLUME or > Cherry.MAX_VOLUME and not Cherry.EARRAPE_VOLUME)
        {
            await ReplyAsync("Please enter a valid volume value");
            return;
        }

        await player.ApplyFilterAsync(Cherry.EmptyFilter, volume);

        switch (((double)player.Volume) * 100)
        {
            case 0:
                await ReplyAsync("**Muted** \\🔇");
                break;

            case 500:
                await ReplyAsync("My volume is now set to **EARRAPE** \\🤯");
                break;

            default:
                await ReplyAsync($"My volume is now set to **{player.Volume}** {(player.Volume >= (int)(Cherry.STANDARD_VOLUME * 100) ? "\\🔊" : "\\🔉")}");
                break;
        }
    }

    [Command("search", RunMode = RunMode.Async)]
    [Alias("seek")]
    [Summary("Jump to a time value in the video")]
    [Remarks("search <[hh:][mm:]ss>")]
    [RequireContext(ContextType.Guild)]
    public async Task Search([Remainder] string? seekTimeStr = null)
    {
        if (!TimeSpan.TryParseExact(seekTimeStr, "[hh:mm:]ss", null, out var timestamp))
        {
            await ReplyAsync("Please enter a valid timestamp");
            return;
        }

        var voiceState = (IVoiceState)Context.User;

        if (voiceState?.VoiceChannel is null)
        {
            await ReplyAsync("Where are you?");
            return;
        }

        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.VoiceChannel != voiceState.VoiceChannel)
        {
            await ReplyAsync("I'm currently in another channel");
            return;
        }

        if (player.PlayerState is not PlayerState.Playing or PlayerState.Paused)
        {
            await ReplyAsync("I'm not playing anything");
            return;
        }

        if (player.Track.IsStream)
        {
            await ReplyAsync("I can't repeat a livestream");
            return;
        }

        if (!player.Track.CanSeek)
        {
            await ReplyAsync("I can't search in this track");
            return;
        }

        if (player.Track.Duration >= timestamp)
        {
            await ReplyAsync("The timespan is longer than the track itself");
            return;
        }

        await player.SeekAsync(timestamp);
        
        if (timestamp.Seconds == 0)
        {
            await ReplyAsync("Jumped to the beginning");
            return;
        }

        await ReplyAsync($"Jumped to {player.Track.GetFormattedDuration()}");
    }

    [Command("reset", RunMode = RunMode.Async)]
    [Summary("Resets all filters")]
    [Remarks("reset")]
    [RequireContext(ContextType.Guild)]
    public async Task Reset()
    {
        var voiceState = (IVoiceState)Context.User;

        if (voiceState?.VoiceChannel is null)
        {
            await ReplyAsync("Where are you?");
            return;
        }

        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.VoiceChannel != voiceState.VoiceChannel)
        {
            await ReplyAsync("I'm currently in another channel");
            return;
        }

        if (player.PlayerState is not PlayerState.Playing or PlayerState.Paused)
        {
            await ReplyAsync("I'm not playing anything");
            return;
        }

        await player.ApplyFilterAsync(Cherry.EmptyFilter, ((double)player.Volume) / 100);
        await ReplyAsync("Removed all filters");
    }

    [Command("nightcore", RunMode = RunMode.Async)]
    [Alias("nc")]
    [Summary("Applies nightcore filter")]
    [Remarks("nightcore [low/medium/high/ultra]")]
    [RequireContext(ContextType.Guild)]
    public async Task Nightcore([Remainder] string level = "")
    {
        var voiceState = (IVoiceState)Context.User;

        if (voiceState?.VoiceChannel is null)
        {
            await ReplyAsync("Where are you?");
            return;
        }

        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.VoiceChannel != voiceState.VoiceChannel)
        {
            await ReplyAsync("I'm currently in another channel");
            return;
        }

        if (player.PlayerState is not PlayerState.Playing or PlayerState.Paused)
        {
            await ReplyAsync("I'm not playing anything");
            return;
        }

        var filter = level.ToLower() switch
        {
            "low" => Cherry.NightcoreFilterLow,
            "high" => Cherry.NightcoreFilterHigh,
            "ultra" => Cherry.NightcoreFilterUltra,
            _ => Cherry.NightcoreFilterMedium,
        };

        await player.ApplyFilterAsync(filter, ((double)player.Volume) / 100);
        await ReplyAsync("Applied nightcore filter on **"
            + (level is "" ? "MEDIUM" : level.ToUpper()) + "**");
    }

    [Command("daycore", RunMode = RunMode.Async)]
    [Alias("dc")]
    [Summary("Applies daycore filter")]
    [Remarks("daycore [low/medium/high/ultra]")]
    [RequireContext(ContextType.Guild)]
    public async Task Daycore([Remainder] string level = "")
    {
        var voiceState = (IVoiceState)Context.User;

        if (voiceState?.VoiceChannel is null)
        {
            await ReplyAsync("Where are you?");
            return;
        }

        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.VoiceChannel != voiceState.VoiceChannel)
        {
            await ReplyAsync("I'm currently in another channel");
            return;
        }

        if (player.PlayerState is not PlayerState.Playing or PlayerState.Paused)
        {
            await ReplyAsync("I'm not playing anything");
            return;
        }

        var filter = level.ToLower() switch
        {
            "low" => Cherry.DaycoreFilterLow,
            "high" => Cherry.DaycoreFilterHigh,
            "ultra" => Cherry.DaycoreFilterUltra,
            _ => Cherry.DaycoreFilterMedium,
        };

        await player.ApplyFilterAsync(filter, ((double)player.Volume) / 100);
        await ReplyAsync("Applied daycore filter on **"
            + (level is "" ? "MEDIUM" : level.ToUpper()) + "**");
    }

    [Command("smoothing", RunMode = RunMode.Async)]
    [Alias("soft")]
    [Summary("Applies smoothing filter")]
    [Remarks("smoothing [low/medium/high/ultra]")]
    [RequireContext(ContextType.Guild)]
    public async Task Smoothing([Remainder] string level = "")
    {
        var voiceState = (IVoiceState)Context.User;

        if (voiceState?.VoiceChannel is null)
        {
            await ReplyAsync("Where are you?");
            return;
        }

        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.VoiceChannel != voiceState.VoiceChannel)
        {
            await ReplyAsync("I'm currently in another channel");
            return;
        }

        if (player.PlayerState is not PlayerState.Playing or PlayerState.Paused)
        {
            await ReplyAsync("I'm not playing anything");
            return;
        }

        var filter = level.ToLower() switch
        {
            "low" => Cherry.SmoothingFilterLow,
            "high" => Cherry.SmoothingFilterHigh,
            "ultra" => Cherry.SmoothingFilterUltra,
            _ => Cherry.SmoothingFilterMedium,
        };

        await player.ApplyFilterAsync(filter, ((double)player.Volume) / 100);
        await ReplyAsync("Applied smoothing filter on **"
            + (level is "" ? "MEDIUM" : level.ToUpper()) + "**");
    }

    [Command("8D", RunMode = RunMode.Async)]
    [Alias("8d")]
    [Summary("Applies 8D filter")]
    [Remarks("8D")]
    [RequireContext(ContextType.Guild)]
    public async Task EightD()
    {
        var voiceState = (IVoiceState)Context.User;

        if (voiceState?.VoiceChannel is null)
        {
            await ReplyAsync("Where are you?");
            return;
        }

        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.VoiceChannel != voiceState.VoiceChannel)
        {
            await ReplyAsync("I'm currently in another channel");
            return;
        }

        if (player.PlayerState is not PlayerState.Playing or PlayerState.Paused)
        {
            await ReplyAsync("I'm not playing anything");
            return;
        }

        await player.ApplyFilterAsync(Cherry.EightDFilter, ((double)player.Volume) / 100);
        await ReplyAsync("Applied 8D filter");
    }

    [Command("lyrics", RunMode = RunMode.Async)]
    [Summary("Get the lyrics from what is currently playing")]
    [Remarks("lyrics")]
    [RequireContext(ContextType.Guild)]
    public async Task Lyrics()
    {
        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            await ReplyAsync("I'm not in any channel");
            return;
        }

        var player = _lavaNode.GetPlayer(Context.Guild);

        if (player.PlayerState is not PlayerState.Playing or PlayerState.Paused)
        {
            await ReplyAsync("I'm not playing anything");
            return;
        }

        await _embed.SendLyricsAsync(player.TextChannel, player.Track);
    }
}