namespace GuessTheLyrics.Domain.Exceptions;

/// <summary>The database could not be reached (after EF Core's retries). Temporary (HTTP 503).</summary>
public sealed class DatabaseUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);
