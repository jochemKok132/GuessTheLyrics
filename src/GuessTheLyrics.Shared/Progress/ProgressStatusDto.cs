using System.Text.Json.Serialization;

namespace GuessTheLyrics.Shared.Progress;

// Serialized as text ("Completed") everywhere, so the API and the UI agree without extra JSON settings.
[JsonConverter(typeof(JsonStringEnumConverter<ProgressStatusDto>))]
public enum ProgressStatusDto
{
    InProgress,
    Completed,
    GaveUp
}
