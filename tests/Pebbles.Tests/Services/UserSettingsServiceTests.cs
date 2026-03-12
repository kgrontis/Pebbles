using Pebbles.Services;

namespace Pebbles.Tests.Services;
public class UserSettingsServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private bool isDisposed;

    public UserSettingsServiceTests()
    {
        // Create a unique temp directory for each test
        _testDirectory = Path.Combine(Path.GetTempPath(), $"pebbles_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void Settings_ReturnsDefaultValues_WhenNoSettingsFile()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert
        Assert.Equal("mock", service.Settings.Provider);
        Assert.False(service.Settings.SetupCompleted);
    }

    [Fact]
    public async Task SaveAsync_CreatesSettingsFile()
    {
        // Arrange
        var service = CreateService();
        service.Settings.Provider = "alibabacloud";
        service.Settings.SetupCompleted = true;

        // Act
        await service.SaveAsync();

        // Assert
        var settingsFile = Path.Combine(_testDirectory, "user_settings.json");
        Assert.True(File.Exists(settingsFile));
    }

    [Fact]
    public async Task SaveAsync_PersistsSettings()
    {
        // Arrange
        var service1 = CreateService();
        service1.Settings.Provider = "openai";
        service1.Settings.SetupCompleted = true;

        // Act
        await service1.SaveAsync();

        // Create a new service instance to verify persistence
        var service2 = CreateService();

        // Assert
        Assert.Equal("openai", service2.Settings.Provider);
        Assert.True(service2.Settings.SetupCompleted);
    }

    [Fact]
    public async Task SetProviderAsync_UpdatesProviderAndSetupCompleted()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.SetProviderAsync("anthropic");

        // Assert
        Assert.Equal("anthropic", service.Settings.Provider);
        Assert.True(service.Settings.SetupCompleted);
    }

    [Fact]
    public void GetApiKey_ReturnsEnvironmentVariable_WhenSet()
    {
        // Arrange
        var service = CreateService();
        var testKey = "test-api-key-12345";
        Environment.SetEnvironmentVariable("ALIBABA_CLOUD_API_KEY", testKey);

        try
        {
            // Act
            var apiKey = service.GetApiKey("alibabacloud");

            // Assert
            Assert.Equal(testKey, apiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ALIBABA_CLOUD_API_KEY", null);
        }
    }

    [Fact]
    public void GetApiKey_ReturnsNull_WhenNotSet()
    {
        // Arrange
        var service = CreateService();
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

        // Act
        var apiKey = service.GetApiKey("openai");

        // Assert
        Assert.Null(apiKey);
    }

    [Fact]
    public void SetApiKey_SetsEnvironmentVariable()
    {
        // Arrange
        var service = CreateService();
        var testKey = "test-key-67890";

        try
        {
            // Act
            service.SetApiKey("openai", testKey);

            // Assert
            Assert.Equal(testKey, Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
        }
    }

    [Fact]
    public void HasValidApiKey_ReturnsTrue_WhenKeyIsSet()
    {
        // Arrange
        var service = CreateService();
        service.Settings.Provider = "anthropic";
        var testKey = "test-anthropic-key";
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", testKey);

        try
        {
            // Act
            var hasKey = service.HasValidApiKey();

            // Assert
            Assert.True(hasKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        }
    }

    [Fact]
    public void HasValidApiKey_ReturnsFalse_WhenKeyNotSet()
    {
        // Arrange
        var service = CreateService();
        service.Settings.Provider = "alibabacloud";
        Environment.SetEnvironmentVariable("ALIBABA_CLOUD_API_KEY", null);

        // Act
        var hasKey = service.HasValidApiKey();

        // Assert
        Assert.False(hasKey);
    }

    [Fact]
    public void LoadSettings_HandlesCorruptedJson()
    {
        // Arrange
        var settingsFile = Path.Combine(_testDirectory, "user_settings.json");
        File.WriteAllText(settingsFile, "{ invalid json }");

        // Act - Should not throw, returns default settings
        var service = CreateService();

        // Assert
        Assert.Equal("mock", service.Settings.Provider);
    }

    [Fact]
    public void GetApiKey_ReturnsNull_ForUnknownProvider()
    {
        // Arrange
        var service = CreateService();

        // Act
        var apiKey = service.GetApiKey("unknown-provider");

        // Assert
        Assert.Null(apiKey);
    }

    private TestableUserSettingsService CreateService()
    {
        return new TestableUserSettingsService(_testDirectory);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }
        if (disposing)
        {
            // Cleanup test directory
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }

            // Clear any test environment variables
            Environment.SetEnvironmentVariable("ALIBABA_CLOUD_API_KEY", null);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        }
        isDisposed = true;
    }

    /// <summary>
    /// Testable version of UserSettingsService that uses a custom directory.
    /// </summary>
    private sealed class TestableUserSettingsService(string testDirectory) : UserSettingsService(testDirectory);
}