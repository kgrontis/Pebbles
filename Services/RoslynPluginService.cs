namespace Pebbles.Services;

using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pebbles.Models;

/// <summary>
/// Compiles and loads C# plugins using Roslyn.
/// </summary>
public sealed class RoslynPluginService
{
    private readonly string _workingDirectory;

    public RoslynPluginService()
    {
        _workingDirectory = Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Compile and load a C# plugin file.
    /// </summary>
    /// <param name="sourcePath">Path to the .cs file</param>
    /// <returns>Loaded plugin instance or null if compilation failed</returns>
    public (CSharpPlugin? Plugin, string? Error) LoadPlugin(string sourcePath)
    {
        try
        {
            var sourceCode = File.ReadAllText(sourcePath);
            var plugin = CompileAndLoad(sourceCode, sourcePath);
            return (plugin, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>
    /// Compile C# source code and load the resulting assembly.
    /// </summary>
    private CSharpPlugin? CompileAndLoad(string sourceCode, string sourcePath)
    {
        // Get references to all required assemblies
        var references = GetMetadataReferences();

        // Parse the source code
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, path: sourcePath);

        // Create compilation
        var assemblyName = $"Plugin_{Path.GetFileNameWithoutExtension(sourcePath)}_{Guid.NewGuid():N}";
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        // Compile to memory
        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"  Line {d.Location.GetLineSpan().StartLinePosition.Line + 1}: {d.GetMessage()}")
                .ToList();

            throw new CompilationException($"Compilation failed:\n{string.Join("\n", errors)}");
        }

        // Load the assembly
        ms.Seek(0, SeekOrigin.Begin);
        var assemblyLoadContext = new PluginLoadContext();
        var assembly = assemblyLoadContext.LoadFromStream(ms);

        // Find PluginBase-derived class
        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(global::Pebbles.Plugins.PluginBase).IsAssignableFrom(t) && !t.IsAbstract);

        if (pluginType is null)
        {
            throw new InvalidOperationException("No class inheriting from PluginBase found in the plugin.");
        }

        // Create instance
        var instance = Activator.CreateInstance(pluginType) as global::Pebbles.Plugins.PluginBase;
        if (instance is null)
        {
            throw new InvalidOperationException("Failed to create plugin instance.");
        }

        return new CSharpPlugin
        {
            Name = instance.Name,
            Version = instance.Version,
            Description = instance.Description,
            SourcePath = sourcePath,
            Instance = instance
        };
    }

    /// <summary>
    /// Get metadata references for compilation.
    /// </summary>
    private static List<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>();

        // Get all loaded assemblies
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .ToList();

        // Add essential assemblies
        var essentialAssemblies = new[]
        {
            typeof(object).Assembly,                           // System.Runtime
            typeof(Console).Assembly,                          // System.Console
            typeof(File).Assembly,                             // System.IO.FileSystem
            typeof(IEnumerable<>).Assembly,                    // System.Collections.Generic
            typeof(global::Pebbles.Plugins.PluginBase).Assembly, // Pebbles (for PluginBase)
        };

        foreach (var assembly in essentialAssemblies)
        {
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        // Add all other loaded assemblies that have a location
        foreach (var assembly in assemblies)
        {
            if (!references.Any(r => r.Display == assembly.Location))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
                catch
                {
                    // Skip assemblies that can't be referenced
                }
            }
        }

        // Add System.Runtime explicitly (needed for some environments)
        var systemRuntime = assemblies.FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        if (systemRuntime is not null && !string.IsNullOrEmpty(systemRuntime.Location))
        {
            if (!references.Any(r => r.Display == systemRuntime.Location))
            {
                references.Add(MetadataReference.CreateFromFile(systemRuntime.Location));
            }
        }

        return references;
    }
}

/// <summary>
/// Custom AssemblyLoadContext for plugin isolation.
/// </summary>
file sealed class PluginLoadContext : AssemblyLoadContext
{
    public PluginLoadContext() : base(isCollectible: true)
    {
    }
}

/// <summary>
/// Exception thrown when plugin compilation fails.
/// </summary>
public sealed class CompilationException : Exception
{
    public CompilationException(string message) : base(message)
    {
    }
}