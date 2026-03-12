namespace Pebbles.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Pebbles.Models;
using Pebbles.Services;
using Pebbles.Services.Tools;
using System.Collections.ObjectModel;

[MemoryDiagnoser]
internal sealed class ToolBenchmarks
{
    private readonly IFileService _fileService;
    private readonly WriteFileTool _writeFileTool;
    private readonly ReadFileTool _readFileTool;
    private readonly ListDirectoryTool _listDirectoryTool;
    private readonly ShellTool _shellTool;
    private readonly string _testDirectory;
    private readonly string _testFile;

    public ToolBenchmarks()
    {
        _fileService = new FileService();
        _writeFileTool = new WriteFileTool();
        _readFileTool = new ReadFileTool(_fileService);
        _listDirectoryTool = new ListDirectoryTool(_fileService);
        _shellTool = new ShellTool();
        
        _testDirectory = Path.Combine(Path.GetTempPath(), $"bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _testFile = Path.Combine(_testDirectory, "benchmark.txt");
        
        // Pre-create test file
        File.WriteAllText(_testFile, "Benchmark test content");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch { }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    [Benchmark]
    public async Task WriteFileTool_Execute()
    {
        var args = System.Text.Json.JsonSerializer.Serialize(new
        {
            path = Path.Combine(_testDirectory, $"bench_{Guid.NewGuid():N}.txt"),
            content = "Benchmark content"
        });
        await _writeFileTool.ExecuteAsync(args).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task ReadFileTool_Execute()
    {
        var args = System.Text.Json.JsonSerializer.Serialize(new { path = _testFile });
        await _readFileTool.ExecuteAsync(args).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task ListDirectoryTool_Execute()
    {
        var args = System.Text.Json.JsonSerializer.Serialize(new { path = _testDirectory });
        await _listDirectoryTool.ExecuteAsync(args).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task ShellTool_Execute()
    {
        var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        var args = System.Text.Json.JsonSerializer.Serialize(new
        {
            command = isWindows ? "echo test" : "echo test"
        });
        await _shellTool.ExecuteAsync(args).ConfigureAwait(false);
    }
}

internal sealed class CompressionBenchmarks
{
    private CompressionService? _compressionService;
    private Collection<ChatMessage> _messages = null!;

    [GlobalSetup]
    public void Setup()
    {
        var aiProvider = new MockAIProvider();
        var promptService = new SystemPromptService();
        var options = new Configuration.PebblesOptions
        {
            DefaultModel = "test-model",
            Provider = "mock"
        };
        _compressionService = new CompressionService(aiProvider, promptService, options);
        _messages = [];

        // Create 20 messages to simulate a long conversation
        for (var i = 0; i < 20; i++)
        {
            _messages.Add(ChatMessage.User($"User message {i} with some content to make it longer", 100));
            _messages.Add(ChatMessage.Assistant($"Assistant response {i} with detailed explanation", 200));
        }
    }

    [Benchmark]
    public async Task Compress_Messages()
    {
        if (_compressionService is not null)
        {
            await _compressionService.CompactAsync(_messages, 6, null).ConfigureAwait(false);
        }
    }
}

internal sealed class FileServiceBenchmarks
{
    private FileService? _fileService;
    private string _testDirectory = null!;
    private string _largeFile = null!;

    [GlobalSetup]
    public void Setup()
    {
        _fileService = new FileService();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"fs_bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        // Create a 100KB file
        _largeFile = Path.Combine(_testDirectory, "large.txt");
        var content = new string('x', 100 * 1024);
        File.WriteAllText(_largeFile, content);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch { }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    [Benchmark]
    public void ReadFile_Small()
    {
        var smallFile = Path.Combine(_testDirectory, "small.txt");
        File.WriteAllText(smallFile, "Small content");
        _fileService?.ReadFile(smallFile);
    }

    [Benchmark]
    public void ReadFile_Large()
    {
        _fileService?.ReadFile(_largeFile);
    }

    [Benchmark]
    public void ListDirectory()
    {
        _fileService?.ListDirectory(_testDirectory);
    }
}

internal static class Program
{
    public static void Main(string[] _)
    {
        BenchmarkRunner.Run<ToolBenchmarks>();
        BenchmarkRunner.Run<CompressionBenchmarks>();
        BenchmarkRunner.Run<FileServiceBenchmarks>();
    }
}
