namespace Pebbles.Services;

using Pebbles.Models;

/// <summary>
/// Mock AI provider for testing and development.
/// </summary>
public class MockAIProvider : IAIProvider
{
    private static readonly Random _rng = new();
    private readonly List<ChatMessage> _conversationHistory = [];
    private readonly ContextManager _contextManager;
    private readonly IFileService _fileService;

    public MockAIProvider(ContextManager contextManager, IFileService fileService)
    {
        _contextManager = contextManager;
        _fileService = fileService;
    }

    private static readonly List<MockResponse> _responses =
    [
        new()
        {
            Keywords = ["hello", "hi", "hey", "greet"],
            Thinking = "The user is greeting me. I should respond warmly and offer to help with their task.",
            ThinkingDuration = TimeSpan.FromSeconds(1.2),
            Content = """
            Hey! 👋 I'm **Pebbles**, your AI coding assistant in the terminal.

            I can help you with:
            - Writing and reviewing code
            - Debugging issues
            - Explaining concepts
            - Refactoring and optimization

            What would you like to work on today?
            """
        },
        new()
        {
            Keywords = ["recursion", "recursive"],
            Thinking = """
            The user is asking about recursion. Let me provide a clear explanation with a practical
            C# code example. I should cover the base case and recursive case, and mention common
            pitfalls like stack overflow. I'll use a factorial example since it's the classic
            illustration, then show a more practical tree traversal example.
            """,
            ThinkingDuration = TimeSpan.FromSeconds(3.8),
            Content = """
            # Recursion in C#

            Recursion is when a function **calls itself** to solve smaller subproblems. Every recursive function needs:

            1. **Base case** — stops the recursion
            2. **Recursive case** — breaks the problem down

            ## Simple Example: Factorial

            ```csharp
            int Factorial(int n)
            {
                if (n <= 1) return 1;        // base case
                return n * Factorial(n - 1);  // recursive case
            }

            Console.WriteLine(Factorial(5)); // 120
            ```

            ## Practical Example: Directory Tree

            ```csharp
            void PrintTree(string path, string indent = "")
            {
                var name = Path.GetFileName(path);
                Console.WriteLine($"{indent}📁 {name}");

                foreach (var dir in Directory.GetDirectories(path))
                    PrintTree(dir, indent + "  ");

                foreach (var file in Directory.GetFiles(path))
                    Console.WriteLine($"{indent}  📄 {Path.GetFileName(file)}");
            }
            ```

            ## ⚠️ Watch Out For

            | Issue | Solution |
            |-------|----------|
            | Stack overflow | Add depth limits or switch to iterative |
            | Duplicate work | Use memoization / caching |
            | Missing base case | Always define termination condition |

            > **Tip:** C# doesn't optimize tail recursion. For deep recursion, consider converting to an iterative approach with an explicit `Stack<T>`.
            """
        },
        new()
        {
            Keywords = ["sort", "algorithm", "sorting"],
            Thinking = "The user wants to know about sorting. I'll explain the most common algorithms with their complexities and provide a quicksort implementation in C#.",
            ThinkingDuration = TimeSpan.FromSeconds(2.5),
            Content = """
            # Sorting Algorithms Overview

            Here are the most common sorting algorithms and their characteristics:

            | Algorithm | Best | Average | Worst | Space | Stable |
            |-----------|------|---------|-------|-------|--------|
            | Bubble Sort | O(n) | O(n²) | O(n²) | O(1) | ✅ |
            | Quick Sort | O(n log n) | O(n log n) | O(n²) | O(log n) | ❌ |
            | Merge Sort | O(n log n) | O(n log n) | O(n log n) | O(n) | ✅ |
            | Heap Sort | O(n log n) | O(n log n) | O(n log n) | O(1) | ❌ |

            ## QuickSort in C#

            ```csharp
            void QuickSort(int[] arr, int low, int high)
            {
                if (low >= high) return;

                int pivot = arr[high];
                int i = low - 1;

                for (int j = low; j < high; j++)
                {
                    if (arr[j] < pivot)
                    {
                        i++;
                        (arr[i], arr[j]) = (arr[j], arr[i]);
                    }
                }

                (arr[i + 1], arr[high]) = (arr[high], arr[i + 1]);
                int pi = i + 1;

                QuickSort(arr, low, pi - 1);
                QuickSort(arr, pi + 1, high);
            }
            ```

            For most real-world C# code, just use **`Array.Sort()`** or **LINQ's `.OrderBy()`** — they use an optimized *introspective sort* internally.
            """
        },
        new()
        {
            Keywords = ["async", "await", "task", "asynchronous"],
            Thinking = "The user is asking about async/await in C#. This is a crucial topic. I need to explain the state machine, task-based pattern, and common pitfalls like deadlocks and async void.",
            ThinkingDuration = TimeSpan.FromSeconds(2.9),
            Content = """
            # Async/Await in C#

            The `async`/`await` pattern enables **non-blocking I/O** without callbacks or manual threading.

            ## How It Works

            ```
            Main Thread          Async Operation
            ────────────         ───────────────
            Call FetchAsync() ──→ Start HTTP request
            (freed!)              ... waiting for network ...
            ... do other work ... ← Response arrives
            Continue after await   
            ```

            ## Basic Pattern

            ```csharp
            public async Task<string> FetchDataAsync(string url)
            {
                using var client = new HttpClient();
                var response = await client.GetStringAsync(url);
                return response;
            }
            ```

            ## Common Mistakes

            1. **`async void`** — Only for event handlers! Use `async Task` instead
            2. **`.Result` / `.Wait()`** — Can deadlock. Always `await`
            3. **Not using `ConfigureAwait(false)`** in libraries

            ```csharp
            // ❌ BAD: Can deadlock
            var result = FetchDataAsync("https://api.example.com").Result;

            // ✅ GOOD: Properly awaited
            var result = await FetchDataAsync("https://api.example.com");
            ```

            ## Parallel Async

            ```csharp
            var task1 = FetchDataAsync("https://api1.example.com");
            var task2 = FetchDataAsync("https://api2.example.com");

            var results = await Task.WhenAll(task1, task2);
            ```

            > **Pro tip:** Use `IAsyncEnumerable<T>` for streaming data — perfect for processing items as they arrive.
            """
        },
        new()
        {
            Keywords = ["pattern", "design", "solid"],
            Thinking = "Design patterns question. I'll cover the most practical patterns for C# developers with concrete examples rather than abstract theory.",
            ThinkingDuration = TimeSpan.FromSeconds(2.1),
            Content = """
            # Essential Design Patterns for C#

            ## 1. Repository Pattern

            ```csharp
            public interface IRepository<T> where T : class
            {
                Task<T?> GetByIdAsync(int id);
                Task<IEnumerable<T>> GetAllAsync();
                Task AddAsync(T entity);
                Task UpdateAsync(T entity);
                Task DeleteAsync(int id);
            }
            ```

            ## 2. Builder Pattern

            ```csharp
            var config = new AppConfigBuilder()
                .WithDatabase("postgres://localhost/mydb")
                .WithCaching(TimeSpan.FromMinutes(5))
                .WithRetryPolicy(maxRetries: 3)
                .Build();
            ```

            ## 3. Strategy Pattern

            ```csharp
            public interface INotificationStrategy
            {
                Task SendAsync(string message, string recipient);
            }

            // Swap implementations at runtime
            services.AddScoped<INotificationStrategy, EmailStrategy>();
            // or
            services.AddScoped<INotificationStrategy, SlackStrategy>();
            ```

            ## When to Use What

            | Pattern | Use When |
            |---------|----------|
            | **Repository** | Abstracting data access |
            | **Builder** | Complex object construction |
            | **Strategy** | Swappable algorithms |
            | **Observer** | Event-driven communication |
            | **Factory** | Object creation logic varies |

            Keep it simple — don't add patterns just because you can. **YAGNI** (You Ain't Gonna Need It) applies to patterns too.
            """
        },
        new()
        {
            Keywords = ["file", "read", "write", "io"],
            Thinking = "File I/O question. I'll show modern C# approaches including async file operations and the newer File API methods.",
            ThinkingDuration = TimeSpan.FromSeconds(1.5),
            Content = """
            # File I/O in Modern C#

            ## Reading Files

            ```csharp
            // Read all text (small files)
            string content = await File.ReadAllTextAsync("data.txt");

            // Read lines (medium files)
            string[] lines = await File.ReadAllLinesAsync("data.txt");

            // Stream (large files)
            await foreach (var line in File.ReadLinesAsync("huge.txt"))
            {
                Process(line);
            }
            ```

            ## Writing Files

            ```csharp
            // Write all at once
            await File.WriteAllTextAsync("output.txt", content);

            // Append
            await File.AppendAllTextAsync("log.txt", $"{DateTime.Now}: Event\n");

            // Stream writing
            await using var writer = new StreamWriter("output.txt");
            await writer.WriteLineAsync("Hello, file!");
            ```

            ## JSON Serialization

            ```csharp
            using System.Text.Json;

            // Write
            var json = JsonSerializer.Serialize(myObject, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync("config.json", json);

            // Read
            var obj = JsonSerializer.Deserialize<MyConfig>(
                await File.ReadAllTextAsync("config.json"));
            ```

            > Always use `async` file APIs in server/UI apps to avoid blocking threads.
            """
        }
    ];

    private static readonly MockResponse _defaultResponse = new()
    {
        Keywords = [],
        Thinking = "Let me think about how to best answer this question. I'll analyze the request and provide a comprehensive, helpful response with code examples where appropriate.",
        ThinkingDuration = TimeSpan.FromSeconds(2.0),
        Content = """
        I understand you're asking about that topic. Here's what I can tell you:

        While I'm running in **mock mode** right now, in a real setup I'd be connected to an AI model that can help with:

        - **Code generation** and review
        - **Debugging** and error analysis
        - **Architecture** discussions
        - **Documentation** writing

        Try asking me about specific topics like:
        - `recursion` — I'll explain with C# examples
        - `async/await` — async patterns in .NET
        - `sorting algorithms` — with complexity analysis
        - `design patterns` — practical C# patterns

        Or type `/help` to see available commands.
        """
    };

    public MockResponse GetResponse(string userInput)
    {
        var lower = userInput.ToLowerInvariant();
        return _responses.FirstOrDefault(r =>
            r.Keywords.Any(k => lower.Contains(k))) ?? _defaultResponse;
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(string userInput, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Mock doesn't actually stream from API, just simulates it
        var response = GetResponse(userInput);
        await foreach (var chunk in StreamContentAsync(response))
        {
            yield return chunk;
        }
    }

    public void AddToHistory(ChatMessage message)
    {
        _conversationHistory.Add(message);
    }

    public void ClearHistory()
    {
        _conversationHistory.Clear();
    }

    public async IAsyncEnumerable<string> StreamThinkingAsync(MockResponse response)
    {
        var words = response.Thinking.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            yield return word + " ";
            await Task.Delay(_rng.Next(15, 45));
        }
    }

    public async IAsyncEnumerable<string> StreamContentAsync(MockResponse response)
    {
        var chars = response.Content.TrimStart('\n').TrimEnd();
        var buffer = "";
        foreach (var c in chars)
        {
            buffer += c;
            // Emit at word boundaries or special chars for natural feel
            if (c is ' ' or '\n' or '.' or ',' or ';' or ':' or '|' or '`' or '#' or '-' or '*')
            {
                yield return buffer;
                buffer = "";
                await Task.Delay(_rng.Next(5, 25));
            }
        }
        if (buffer.Length > 0)
            yield return buffer;
    }
}

public class MockResponse
{
    public List<string> Keywords { get; init; } = [];
    public string Thinking { get; init; } = string.Empty;
    public TimeSpan ThinkingDuration { get; init; }
    public string Content { get; init; } = string.Empty;
}
