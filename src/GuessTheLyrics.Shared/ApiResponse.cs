namespace GuessTheLyrics.Shared;

public sealed class ApiResponse<T>
{
    public T? Value { get; init; }
    public bool Succeeded { get; init; }
    public string? Message { get; init; }

    /// <summary>Machine-readable reason for a failure, one of <see cref="ErrorCodes"/>. Empty on success.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Request id that is also in the API log, so a reported error can be found back. Only on failures.</summary>
    public string? TraceId { get; init; }

    public static ApiResponse<T> Ok(T value, string? message = null) =>
        new() { Value = value, Succeeded = true, Message = message };

    public static ApiResponse<T> Fail(string message, string? errorCode = null, string? traceId = null) =>
        new() { Succeeded = false, Message = message, ErrorCode = errorCode, TraceId = traceId };
}
