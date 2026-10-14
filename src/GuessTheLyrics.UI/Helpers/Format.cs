namespace GuessTheLyrics.UI.Helpers;

public static class Format
{
    public static string Time(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");

    public static string Seconds(int? seconds) =>
        seconds is null ? "–" : Time(TimeSpan.FromSeconds(seconds.Value));

    public static string Duration(int seconds) =>
        seconds <= 0 ? "" : $"{seconds / 60}:{seconds % 60:00}";

    public static string Relative(DateTime? utc)
    {
        if (utc is null)
            return "never";

        var ago = DateTime.UtcNow - DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc);

        return ago.TotalMinutes switch
        {
            < 1 => "just now",
            < 60 => $"{(int)ago.TotalMinutes} min ago",
            < 60 * 24 => $"{(int)ago.TotalHours} h ago",
            < 60 * 24 * 30 => $"{(int)ago.TotalDays} d ago",
            _ => utc.Value.ToLocalTime().ToString("d MMM yyyy")
        };
    }
}
