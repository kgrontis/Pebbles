namespace Pebbles.Tests.Services;

using Pebbles.Services;
using Polly;

public class RetryPoliciesTests
{
    [Fact]
    public void GetFileIoPolicy_ReturnsPolicy()
    {
        // Act
        var policy = RetryPolicies.GetFileIoPolicy(maxRetries: 3);

        // Assert
        Assert.NotNull(policy);
    }

    [Fact]
    public async Task GetFileIoPolicy_RetriesOnIOException()
    {
        // Arrange
        var policy = RetryPolicies.GetFileIoPolicy(maxRetries: 2);
        var retryCount = 0;

        // Act
        await policy.ExecuteAsync(() =>
        {
            retryCount++;
            if (retryCount < 3)
                throw new IOException("File locked");
            
            return Task.CompletedTask;
        });

        // Assert
        Assert.Equal(3, retryCount);
    }

    [Fact]
    public async Task GetFileIoPolicy_RetriesOnUnauthorizedAccessException()
    {
        // Arrange
        var policy = RetryPolicies.GetFileIoPolicy(maxRetries: 2);
        var retryCount = 0;

        // Act
        await policy.ExecuteAsync(() =>
        {
            retryCount++;
            if (retryCount < 3)
                throw new UnauthorizedAccessException("Access denied");
            
            return Task.CompletedTask;
        });

        // Assert
        Assert.Equal(3, retryCount);
    }

    [Fact]
    public void GetShellCommandPolicy_ReturnsPolicy()
    {
        // Act
        var policy = RetryPolicies.GetShellCommandPolicy(maxRetries: 3);

        // Assert
        Assert.NotNull(policy);
    }

    [Fact]
    public async Task GetShellCommandPolicy_DoesNotRetryOnOperationCanceled()
    {
        // Arrange
        var policy = RetryPolicies.GetShellCommandPolicy(maxRetries: 3);
        var retryCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await policy.ExecuteAsync(async ct =>
            {
                retryCount++;
                await Task.CompletedTask;
                throw new OperationCanceledException();
            }, CancellationToken.None);
        });

        Assert.Equal(1, retryCount);
    }

    [Fact]
    public void GetApiPolicy_ReturnsPolicy()
    {
        // Act
        var policy = RetryPolicies.GetApiPolicy(maxRetries: 5);

        // Assert
        Assert.NotNull(policy);
    }

    [Fact]
    public async Task GetApiPolicy_RetriesOnHttpRequestException()
    {
        // Arrange
        var policy = RetryPolicies.GetApiPolicy(maxRetries: 2);
        var retryCount = 0;

        // Act
        await policy.ExecuteAsync(() =>
        {
            retryCount++;
            if (retryCount < 3)
                throw new HttpRequestException("Network error");
            
            return Task.CompletedTask;
        });

        // Assert
        Assert.Equal(3, retryCount);
    }
}