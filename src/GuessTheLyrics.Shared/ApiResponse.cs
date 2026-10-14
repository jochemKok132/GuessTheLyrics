namespace GuessTheLyrics.Shared;

public sealed class ApiResponse<T>
{
    public T? Value { get; init; }
    public bool Succeeded { get; init; }
    public string? Message { get; init; }

    public static ApiResponse<T> Ok(T value, string? message = null) =>
        new() { Value = value, Succeeded = true, Message = message };

    public static ApiResponse<T> Fail(string message) =>
        new() { Succeeded = false, Message = message };
}
