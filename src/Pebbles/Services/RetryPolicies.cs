namespace Pebbles.Services;

using Polly;
using Polly.Retry;

/// <summary>
/// Provides retry policies for resilient operations.
/// </summary>
internal static class RetryPolicies
{
    /// <summary>
    /// Gets a retry policy for file I/O operations.
    /// Retries on transient errors like file locks, with exponential backoff.
    /// </summary>
    public static AsyncRetryPolicy GetFileIoPolicy(int maxRetries = 3)
    {
        return Policy
            .Handle<IOException>()
            .Or<UnauthorizedAccessException>()
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: retry => TimeSpan.FromMilliseconds(Math.Pow(2, retry) * 100),
                onRetry: (exception, timeSpan, retryNumber, context) =>
                {
                    Console.WriteLine($"[RETRY] File operation failed: {exception.Message}. Retry {retryNumber}/{maxRetries} in {timeSpan.TotalMilliseconds}ms");
                });
    }

    /// <summary>
    /// Gets a retry policy for shell command execution.
    /// Retries on transient errors with exponential backoff.
    /// </summary>
    public static AsyncRetryPolicy GetShellCommandPolicy(int maxRetries = 3)
    {
        return Policy
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: retry => TimeSpan.FromMilliseconds(Math.Pow(2, retry) * 200),
                onRetry: (exception, timeSpan, retryNumber, context) =>
                {
                    Console.WriteLine($"[RETRY] Command failed: {exception.Message}. Retry {retryNumber}/{maxRetries} in {timeSpan.TotalMilliseconds}ms");
                });
    }

    /// <summary>
    /// Gets a retry policy for AI API calls.
    /// Retries on rate limits and transient network errors.
    /// </summary>
    public static AsyncRetryPolicy GetApiPolicy(int maxRetries = 5)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>(ex => ex.InnerException is TimeoutException)
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: retry => TimeSpan.FromSeconds(Math.Pow(2, retry)),
                onRetry: (exception, timeSpan, retryNumber, context) =>
                {
                    Console.WriteLine($"[RETRY] API call failed: {exception.Message}. Retry {retryNumber}/{maxRetries} in {timeSpan.TotalSeconds}s");
                });
    }
}