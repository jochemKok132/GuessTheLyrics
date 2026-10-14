using System.Text.Json.Serialization;

namespace GuessTheLyrics.Shared.Matches;

[JsonConverter(typeof(JsonStringEnumConverter<MatchStatusDto>))]
public enum MatchStatusDto
{
    /// <summary>Waiting for players; the host can start.</summary>
    Lobby,

    /// <summary>Started; the clock runs from <see cref="MatchResponse.StartsAt"/>.</summary>
    Countdown,

    Playing,
    Finished
}

public sealed class CreateMatchRequest
{
    public long SongId { get; set; }
}

public sealed class MatchProgressRequest
{
    /// <summary>All normalised words you have found so far in this match.</summary>
    public List<string> FoundKeys { get; set; } = [];
}

public sealed class MatchResponse
{
    public string Code { get; init; } = string.Empty;
    public long SongId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string? Album { get; init; }
    public int TotalWords { get; init; }
    public int UniqueWords { get; init; }
    public int TimeLimitSeconds { get; init; }
    public int MaxPlayers { get; init; }

    public MatchStatusDto Status { get; init; }
    public string HostUsername { get; init; } = string.Empty;
    public DateTime? StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }

    /// <summary>When the match actually ended: at <see cref="EndsAt"/>, or earlier when everyone was done.</summary>
    public DateTime? FinishedAt { get; init; }

    /// <summary>The server's clock when this answer was made; the UI uses it to correct for a wrong local clock.</summary>
    public DateTime ServerTime { get; init; }

    public bool YouAreIn { get; init; }
    public bool YouAreHost { get; init; }
    public bool CanJoin { get; init; }

    /// <summary>Your own found words, so a refreshed page can continue.</summary>
    public List<string>? YourFoundKeys { get; init; }

    public string? WinnerUsername { get; init; }
    public List<MatchPlayerResponse> Players { get; init; } = [];
}

public sealed class MatchPlayerResponse
{
    public string Username { get; init; } = string.Empty;
    public int Rank { get; init; }
    public int FoundWordCount { get; init; }
    public int Percentage { get; init; }

    /// <summary>Found every word; <see cref="FinishedAfterSeconds"/> says how fast.</summary>
    public bool Finished { get; init; }
    public int? FinishedAfterSeconds { get; init; }
    public bool Left { get; init; }
    public bool IsHost { get; init; }
    public bool IsYou { get; init; }
}

/// <summary>An open lobby in the list of matches you can join.</summary>
public sealed class MatchSummaryResponse
{
    public string Code { get; init; } = string.Empty;
    public long SongId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string HostUsername { get; init; } = string.Empty;
    public int PlayerCount { get; init; }
    public int MaxPlayers { get; init; }
    public int TimeLimitSeconds { get; init; }
    public DateTime CreatedAt { get; init; }
}
