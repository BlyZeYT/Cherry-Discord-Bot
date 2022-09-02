﻿namespace Cherry.Common;

using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using Victoria;
using Victoria.Resolvers;

public static class Extensions
{
    public static void LogDatabaseError(this ILogger logger, Exception? exception, string methodName) => logger.LogError(exception, $"Database Error in: {methodName}", 0);

    public static void LogCherryServerError(this ILogger logger) => logger.LogError("Can't get Cherry Server", 1);

    public static void LogLavaTrackStuckError(this ILogger logger, LavaTrack track, TimeSpan threshold) => logger.LogError($"Track: {track.Title} got stuck for {threshold.TotalMilliseconds}ms", 2);

    public static void LogLavaTrackExceptionError(this ILogger logger, LavaTrack track, LavaException exception) => logger.LogError($"Track: {track.Title} threw an exception: {exception.Message} |-| {exception.Cause}", 3);

    public static void LogFeedback(this ILogger logger, string message) => logger.LogError("Feedback -> " + message);

    public static async Task<string> GetArtworkLinkAsync(this LavaTrack track)
    {
        string link = await track.FetchArtworkAsync();

        return string.IsNullOrWhiteSpace(link)
            || link.Contains("https://soundcloud.com/images/fb_placeholder.png")
            ? Cherry.NOT_FOUND : link;
    }

    public static Task<Tuple<Source, string>> GetSourceInfo(this LavaTrack track)
    {
        string url = track.Url;

        if (url.StartsWith("https://www.youtube.com/", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new Tuple<Source, string>(Source.YouTube, "https://www.youtube.com/"));

        if (url.StartsWith("https://music.youtube.com/", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new Tuple<Source, string>(Source.YouTubeMusic, "https://music.youtube.com/"));

        if (url.StartsWith("https://soundcloud.com/", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new Tuple<Source, string>(Source.Soundcloud, "https://soundcloud.com/"));

        if (url.StartsWith("https://www.twitch.tv/", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new Tuple<Source, string>(Source.Twitch, "https://www.twitch.tv/"));

        return Task.FromResult(new Tuple<Source, string>(Source.Unknown, ""));
    }

    public static async Task<string> GetLyrics(this LavaTrack track)
    {
        string lyrics = await LyricsResolver.SearchGeniusAsync(track);

        if (string.IsNullOrWhiteSpace(lyrics))
            lyrics = await LyricsResolver.SearchOvhAsync(track);

        return lyrics;
    }
    
    public static string GetFormattedDuration(this LavaTrack track)
    {
        var duration = track.Duration;

        return duration.Hours > 0
            ? track.Duration.ToString(@"hh\:mm\:ss") + " h"
            : duration.Minutes > 0 ? track.Duration.ToString(@"mm\:ss") + " m" : track.Duration.ToString("ss") + " s";
    }

    public static bool IsDMChannel(this ISocketMessageChannel channel) => channel is SocketDMChannel;

    public static (T, IEnumerable<T>) GetFirstAndRemainder<T>(this IEnumerable<T> sequence)
    {
        var enumerator = sequence.GetEnumerator();
        enumerator.MoveNext();
        return (enumerator.Current, enumerator.AsEnumerable());
    }

    public static IEnumerable<T> AsEnumerable<T>(this IEnumerator<T> enumerator)
    {
        while (enumerator.MoveNext()) yield return enumerator.Current;
    }

    public static bool IsEmpty(this DefaultQueue<LavaTrack> queue) => queue.Count == 0;

    public static bool TryRemoveAt(this DefaultQueue<LavaTrack> queue, int index, out LavaTrack removedTrack)
    {
        try
        {
            removedTrack = queue.RemoveAt(index);
            return true;
        }
        catch (Exception)
        {
            removedTrack = default!;
            return false;
        }
    }
}