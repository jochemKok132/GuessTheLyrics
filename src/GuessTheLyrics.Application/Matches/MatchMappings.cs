using GuessTheLyrics.Shared.Matches;

namespace GuessTheLyrics.Application.Matches;

public static class MatchMappings
{
    /// <summary>The match as seen by <paramref name="userId"/>. Call inside <c>lock (match.Lock)</c>.</summary>
    public static MatchResponse ToResponse(this Match match, Guid userId, DateTime now)
    {
        var you = match.FindPlayer(userId);
        var youAreIn = you is { Left: false };
        var ranking = match.Ranking();
        var host = match.Players.FirstOrDefault(p => p.UserId == match.HostId);

        var players = ranking.Select((p, index) => new MatchPlayerResponse
        {
            Username = p.Username,
            Rank = index + 1,
            FoundWordCount = p.FoundWordCount,
            Percentage = match.TotalWords == 0 ? 0 : p.FoundWordCount * 100 / match.TotalWords,
            Finished = p.FinishedAt is not null,
            FinishedAfterSeconds = p.FinishedAt is { } finished && match.StartsAt is { } start
                ? (int)Math.Round((finished - start).TotalSeconds)
                : null,
            Left = p.Left,
            IsHost = p.UserId == match.HostId,
            IsYou = p.UserId == userId
        }).ToList();

        var winner = match.Status == MatchStatus.Finished
            ? ranking.FirstOrDefault(p => !p.Left && p.FoundWordCount > 0)
            : null;

        return new MatchResponse
        {
            Code = match.Code,
            SongId = match.SongId,
            Title = match.Title,
            Artist = match.Artist,
            Album = match.Album,
            TotalWords = match.TotalWords,
            UniqueWords = match.UniqueWords,
            TimeLimitSeconds = match.TimeLimitSeconds,
            MaxPlayers = MatchService.MaxPlayers,
            Status = match.Status switch
            {
                MatchStatus.Lobby => MatchStatusDto.Lobby,
                MatchStatus.Playing when now < match.StartsAt => MatchStatusDto.Countdown,
                MatchStatus.Playing => MatchStatusDto.Playing,
                MatchStatus.Finished => MatchStatusDto.Finished,
                _ => throw new ArgumentOutOfRangeException(nameof(match), match.Status, "Unknown match status.")
            },
            HostUsername = host?.Username ?? string.Empty,
            StartsAt = match.StartsAt,
            EndsAt = match.EndsAt,
            FinishedAt = match.FinishedAt,
            ServerTime = now,
            YouAreIn = youAreIn,
            YouAreHost = match.HostId == userId,
            CanJoin = !youAreIn && match.Status == MatchStatus.Lobby && match.Players.Count < MatchService.MaxPlayers,
            YourFoundKeys = youAreIn ? you!.FoundKeys.ToList() : null,
            WinnerUsername = winner?.Username,
            Players = players
        };
    }

    public static MatchSummaryResponse ToSummary(this Match match) => new()
    {
        Code = match.Code,
        SongId = match.SongId,
        Title = match.Title,
        Artist = match.Artist,
        HostUsername = match.Players.FirstOrDefault(p => p.UserId == match.HostId)?.Username ?? string.Empty,
        PlayerCount = match.Players.Count,
        MaxPlayers = MatchService.MaxPlayers,
        TimeLimitSeconds = match.TimeLimitSeconds,
        CreatedAt = match.CreatedAt
    };
}
