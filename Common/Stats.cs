namespace Cherry.Common;

using Victoria.EventArgs;

public static class Stats
{
    public static Task UpdateAsync(Cpu cpu, Frames frames, Memory memory, TimeSpan uptime, int players)
    {
        CPU = cpu;
        Frames = frames;
        Memory = memory;
        Uptime = uptime;
        Players = players;

        return Task.CompletedTask;
    }

    public static Cpu CPU { get; private set; }
    public static Frames Frames { get; private set; }
    public static Memory Memory { get; private set; }
    public static TimeSpan Uptime { get; private set; }
    public static int Players { get; private set; }
}
