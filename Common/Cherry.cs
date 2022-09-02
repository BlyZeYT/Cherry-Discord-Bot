namespace Cherry.Common;

using System.Diagnostics;
using Victoria.Filters;

public static class Cherry
{
    public const ulong SERVER = 987877607116771338;

    public const string NOT_FOUND = "https://i.imgur.com/O5DNR57.png";

    public const string INFORMATION = "https://i.imgur.com/VUCXoJo.png";

    public const string CONTACTMENU_ID = "CherryContactMenu";
    public const string CONTACTMENU_IDEA_ID = "SelectIdea";
    public const string CONTACTMENU_ISSUE_ID = "SelectIssue";
    public const string CONTACTMENU_BUG_ID = "SelectBug";

    public const string INVITATION_DENIED_ID = "DeniedInvitation";

    public const ushort MIN_VOLUME = 0;
    public const ushort STANDARD_VOLUME = 100;
    public const ushort MAX_VOLUME = 200;
    public const ushort EARRAPE_VOLUME = 500;

    public static readonly IFilter EmptyFilter;

    public static readonly IFilter DaycoreFilterLow;
    public static readonly IFilter DaycoreFilterMedium;
    public static readonly IFilter DaycoreFilterHigh;
    public static readonly IFilter DaycoreFilterUltra;

    public static readonly IFilter NightcoreFilterLow;
    public static readonly IFilter NightcoreFilterMedium;
    public static readonly IFilter NightcoreFilterHigh;
    public static readonly IFilter NightcoreFilterUltra;

    public static readonly IFilter SmoothingFilterLow;
    public static readonly IFilter SmoothingFilterMedium;
    public static readonly IFilter SmoothingFilterHigh;
    public static readonly IFilter SmoothingFilterUltra;

    public static readonly IFilter EightDFilter;

    static Cherry()
    {
        EmptyFilter = new TimescaleFilter
        {
            Pitch = 1,
            Rate = 1,
            Speed = 1
        };

        DaycoreFilterLow = new TimescaleFilter
        {
            Pitch = 0.85,
            Rate = 1,
            Speed = 0.85
        };

        DaycoreFilterMedium = new TimescaleFilter
        {
            Pitch = 0.80,
            Rate = 1,
            Speed = 0.80
        };

        DaycoreFilterHigh = new TimescaleFilter
        {
            Pitch = 0.75,
            Rate = 1,
            Speed = 0.75
        };

        DaycoreFilterUltra = new TimescaleFilter
        {
            Pitch = 0.65,
            Rate = 1,
            Speed = 0.65
        };

        NightcoreFilterLow = new TimescaleFilter
        {
            Pitch = 1.15,
            Rate = 1,
            Speed = 1.15
        };

        NightcoreFilterMedium = new TimescaleFilter
        {
            Pitch = 1.20,
            Rate = 1,
            Speed = 1.20
        };

        NightcoreFilterHigh = new TimescaleFilter
        {
            Pitch = 1.25,
            Rate = 1,
            Speed = 1.25
        };

        NightcoreFilterUltra = new TimescaleFilter
        {
            Pitch = 1.30,
            Rate = 1,
            Speed = 1.35
        };

        SmoothingFilterLow = new LowPassFilter
        {
            Smoothing = 1.1
        };

        SmoothingFilterMedium = new LowPassFilter
        {
            Smoothing = 1.35
        };

        SmoothingFilterHigh = new LowPassFilter
        {
            Smoothing = 1.75
        };

        SmoothingFilterUltra = new LowPassFilter
        {
            Smoothing = 2
        };

        EightDFilter = new RotationFilter
        {
            Hertz = 0.2
        };
    }
}
