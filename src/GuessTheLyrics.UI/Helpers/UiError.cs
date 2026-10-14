using GuessTheLyrics.Shared;

namespace GuessTheLyrics.UI.Helpers;

/// <summary>A failed API call, ready to show with the ErrorMessage component.</summary>
public sealed record UiError(string Message, string? ErrorCode, string? TraceId)
{
    public bool IsRetryable => ErrorCodes.IsRetryable(ErrorCode);

    public static UiError From<T>(ApiResponse<T> response) =>
        new(response.Message ?? "Something went wrong. Try again.", response.ErrorCode, response.TraceId);
}
