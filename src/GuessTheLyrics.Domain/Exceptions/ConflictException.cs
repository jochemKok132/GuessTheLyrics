namespace GuessTheLyrics.Domain.Exceptions;

/// <summary>The request clashes with existing data, e.g. a username that is already taken (HTTP 409).</summary>
public sealed class ConflictException(string message, Exception? innerException = null) : Exception(message, innerException);
