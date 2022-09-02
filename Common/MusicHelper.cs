namespace Cherry.Common;

using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using Victoria;
using Victoria.Enums;
using Victoria.Responses.Search;

public class MusicHelper : IMusicHelper
{
    private readonly LavaNode _lavaNode;
    private readonly IEmbedSender _embed;

    public MusicHelper(LavaNode lavaNode, IEmbedSender embed)
    {
        _lavaNode = lavaNode;
        _embed = embed;
    }

    public async ValueTask<LavaPlayer> GetOrJoinPlayerAsync(IGuild guild, IVoiceChannel voiceChannel, ITextChannel textChannel)
    {
        LavaPlayer player;

        if (!_lavaNode.HasPlayer(guild))
        {
            player = await _lavaNode.JoinAsync(voiceChannel, textChannel);
            await player.ApplyFilterAsync(Cherry.EmptyFilter, Cherry.STANDARD_VOLUME);
        }
        else player = _lavaNode.GetPlayer(guild);

        return player;
    }

    public ValueTask<Search> GetSearchAsync(string searchQuery) => ValueTask.FromResult(new Search(searchQuery));

    public async Task PlayMusicAsync(ITextChannel channel, IVoiceState voiceState, SocketGuild guild, string? searchQuery, Source source)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            await channel.SendMessageAsync("Please enter a search term or a link.");
            return;
        }

        if (voiceState?.VoiceChannel is null)
        {
            await channel.SendMessageAsync("Where are you?");
            return;
        }

        var search = await GetSearchAsync(searchQuery);

        var searchResponse = await _lavaNode.SearchAsync(search.GetVictoriaSearchType(source), search);

        if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
        {
            await channel.SendMessageAsync("Couldn't find anything for " + searchQuery);
            return;
        }

        var player = await GetOrJoinPlayerAsync(guild, voiceState.VoiceChannel, channel);

        if (player.VoiceChannel != voiceState.VoiceChannel)
        {
            await channel.SendMessageAsync("I'm currently in another channel");
            return;
        }

        if (player.PlayerState is PlayerState.Playing or PlayerState.Paused)
        {
            if (search.IsPlaylist)
            {
                player.Queue.Enqueue(searchResponse.Tracks);

                await _embed.SendTracksEnqueuedAsync(player.TextChannel, searchResponse.Tracks.ToArray());
            }
            else
            {
                var track = searchResponse.Tracks.First();

                player.Queue.Enqueue(track);

                await _embed.SendTrackEnqueuedAsync(player.TextChannel, track);
            }
        }
        else
        {
            if (search.IsPlaylist)
            {
                var (firstTrack, remainedTracks) = searchResponse.Tracks.GetFirstAndRemainder();

                await player.PlayAsync(firstTrack);

                player.Queue.Enqueue(remainedTracks);

                await _embed.SendTracksEnqueuedAsync(player.TextChannel, remainedTracks.ToArray());
            }
            else await player.PlayAsync(searchResponse.Tracks.First());
        }
    }
}

public interface IMusicHelper
{
    /// <summary>
    /// Checks if guild has already a player; If yes it will return the player, if not it will join and return the new player.
    /// </summary>
    public ValueTask<LavaPlayer> GetOrJoinPlayerAsync(IGuild guild, IVoiceChannel voiceChannel, ITextChannel textChannel);

    public ValueTask<Search> GetSearchAsync(string searchQuery);

    public Task PlayMusicAsync(ITextChannel channel, IVoiceState voiceState, SocketGuild guild, string? searchQuery, Source source);
}

public readonly struct Search
{
    public char[] SearchQuery { get; init; }
    public ExplicitSearchType ExplicitSearchType { get; init; }
    public bool IsPlaylist { get; init; }

    public Search()
    {
        SearchQuery = Array.Empty<char>();
        ExplicitSearchType = ExplicitSearchType.Invalid;
        IsPlaylist = false;
    }

    public Search(string searchQuery)
    {
        ExplicitSearchType = GetSearchTypes(searchQuery);
        SearchQuery = GetFormattedSearchQuery(searchQuery, ExplicitSearchType);
        IsPlaylist = ExplicitSearchType switch
        {
            ExplicitSearchType.YouTubePlaylistLink or
            ExplicitSearchType.YouTubeMusicPlaylistLink or
            ExplicitSearchType.SoundcloudPlaylistLink => true,
            _ => false
        };
    }

    public SearchType GetVictoriaSearchType(Source source)
    {
        return ExplicitSearchType is ExplicitSearchType.Search
            ? source switch
            {
                Source.YouTube => SearchType.YouTube,
                Source.YouTubeMusic => SearchType.YouTubeMusic,
                Source.Soundcloud => SearchType.SoundCloud,
                _ => SearchType.Direct,
            }
            : SearchType.Direct;
    }

    private static char[] GetFormattedSearchQuery(string searchQuery, ExplicitSearchType searchType)
    {
        switch (searchType)
        {
            case ExplicitSearchType.YouTubePlaylistLink:
                if (!searchQuery.Contains("playlist?list"))
                {
                    var start = searchQuery.LastIndexOf("https://www.youtube.com/") + "https://www.youtube.com/".Length;
                    var end = searchQuery.IndexOf("&list", start);
                    string formattedStr = searchQuery.Remove(start, end - start);

                    return formattedStr.Replace("playlist?", "&").ToCharArray();
                }
                break;

            case ExplicitSearchType.YouTubeMusicPlaylistLink:
                if (!searchQuery.Contains("playlist?list"))
                {
                    var start = searchQuery.LastIndexOf("https://music.youtube.com/") + "https://music.youtube.com/".Length;
                    var end = searchQuery.IndexOf("&list", start);
                    string formattedStr = searchQuery.Remove(start, end - start);

                    return formattedStr.Replace("playlist?", "&").ToCharArray();
                }
                break;
        }

        return searchQuery.ToCharArray();
    }

    private static ExplicitSearchType GetSearchTypes(string searchQuery)
    {
        if ((searchQuery.StartsWith("http://") || searchQuery.StartsWith("https://")) && (searchQuery.Contains("youtube.com/watch?v=") || searchQuery.Contains("youtu.be/") || searchQuery.Contains("youtube.com/playlist?list=")) && (searchQuery.Contains("&list=") || searchQuery.Contains("?list=")))
            return ExplicitSearchType.YouTubePlaylistLink;

        if (Regex.IsMatch(searchQuery, @"^(http|https):\/\/(?:www\.)?twitch.com\/[a-zA-Z0-9_]$"))
            return ExplicitSearchType.TwitchStreamLink;

        if ((searchQuery.StartsWith("http://") || searchQuery.StartsWith("https://")) && (searchQuery.Contains("youtube.com/watch?v=") || searchQuery.Contains("youtu.be/")))
            return ExplicitSearchType.YouTubeVideoLink;

        if ((searchQuery.StartsWith("http://music.youtube.com") || searchQuery.StartsWith("https://music.youtube.com")) && (searchQuery.Contains("&list") || searchQuery.Contains("playlist?list")))
            return ExplicitSearchType.YouTubeMusicPlaylistLink;

        if (searchQuery.StartsWith("http://music.youtube.com") || searchQuery.StartsWith("https://music.youtube.com"))
            return ExplicitSearchType.YouTubeMusicLink;

        if (searchQuery.StartsWith("http://soundcloud.com") || searchQuery.StartsWith("https://soundcloud.com"))
        {
            return searchQuery.Contains("/sets/") ? ExplicitSearchType.SoundcloudPlaylistLink : ExplicitSearchType.SoundcloudLink;
        }

        return ExplicitSearchType.Search;
    }

    public static implicit operator string(Search s) => new(s.SearchQuery);
}

public enum ExplicitSearchType
{
    Invalid,
    Search,
    YouTubeVideoLink,
    YouTubePlaylistLink,
    YouTubeMusicLink,
    YouTubeMusicPlaylistLink,
    SoundcloudLink,
    SoundcloudPlaylistLink,
    TwitchStreamLink
}

public enum Source
{
    Unknown,
    YouTube,
    YouTubeMusic,
    Soundcloud,
    Twitch
}