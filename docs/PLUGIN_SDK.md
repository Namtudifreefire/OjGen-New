# OjGen Plugin SDK Reference

The plugin contract has API version `2` (`ProblemPluginSdk.ApiVersion`). A plugin assembly must reference `OjGen.Core`, implement `IOjGenPlugin` (or just `IOjGenProblemPlugin` for basic generators), and return its extensions from the factory methods.

## Core Interfaces

### IOjGenProblemPlugin (required)
```csharp
public interface IOjGenProblemPlugin
{
    string Name { get; }
    IEnumerable<Problem> CreateProblems();
}
```

### IOjGenProblemPluginMetadata (required for API version check)
```csharp
public interface IOjGenProblemPluginMetadata
{
    int ApiVersion { get; }
    string Version { get; }
    string Author { get; }
    string Description { get; }
    string Url { get; }
    string MinOjGenVersion { get; }
}
```

### IOjGenPlugin (recommended)
```csharp
public interface IOjGenPlugin : IOjGenProblemPlugin, IOjGenProblemPluginMetadata
{
    IEnumerable<IOjGenValidator>? CreateValidators() => null;
    IEnumerable<IOjGenChecker>? CreateCheckers() => null;
    IEnumerable<IOjGenExporter>? CreateExporters() => null;
    IEnumerable<IOjGenSolutionRunner>? CreateSolutionRunners() => null;
    IEnumerable<IOjGenEventHook>? CreateEventHooks() => null;
}
```

## Extension Interfaces

### IOjGenValidator — Custom Input Validators
```csharp
public interface IOjGenValidator
{
    string Id { get; }                    // Unique ID: "myplugin.graph-validator"
    string Name { get; }                   // Human name
    IReadOnlyList<string> ApplicableProblemIds { get; }  // Empty = all problems

    // Returns null if valid, error message if invalid
    string? ValidateInput(string input, Config config, Problem problem);
}
```
Use case: Add semantic validation beyond format checks (e.g., verify graph is actually connected, verify chess position is legal).

### IOjGenChecker — Custom Output Checkers
```csharp
public interface IOjGenChecker
{
    string Id { get; }                     // Unique ID: "myplugin.floating-point-checker"
    string Name { get; }
    IReadOnlyList<string> ApplicableProblemIds { get; }
    string ArgumentsTemplate { get; }      // e.g., "{input} {output} {answer}"
    int TimeoutMs { get; }                 // Per-test timeout

    // Returns (isAccepted, message, exitCode) where exitCode: 0=AC, 1=WA, 2=checker error
    (bool Accepted, string Message, int ExitCode) Check(
        string inputPath, string outputPath, string answerPath, Config config, Problem problem);
}
```
Use case: Specialized checkers for floating-point tolerance, multiple valid outputs, custom scoring.

### IOjGenExporter — Custom Test Package Exporters
```csharp
public interface IOjGenExporter
{
    string Id { get; }                     // Unique ID: "myplugin.custom-format"
    string FormatName { get; }             // Human name: "Custom Format"
    string ArchiveExtension { get; }       // e.g., ".zip", ".tar.gz"

    // Returns true on success, sets error on failure
    bool Export(string testDirectory, string outputArchivePath, IJobLog log, out string? error);
}
```
Use case: Export to judge-specific formats not built into OjGen.

### IOjGenSolutionRunner — Custom Solution Runners
```csharp
public interface IOjGenSolutionRunner
{
    string Id { get; }                     // Unique ID: "myplugin.haskell-runner"
    string Name { get; }                   // Human name
    IReadOnlyList<string> HandledExtensions { get; }  // e.g., [".hs", ".lhs"]

    // Prepare solution for execution (compile if needed)
    PreparedSolution Prepare(string solutionPath, Settings settings, string workDir,
                             bool forceRebuild, IJobLog log, CancellationToken ct = default);
}
```
Use case: Support languages not natively handled (Haskell, Swift, Zig, etc.).

### IOjGenEventHook — Lifecycle Event Hooks
```csharp
public interface IOjGenEventHook
{
    void OnStart(IJobLog log);
    void OnBeforeGenerate(Problem problem, Config config, int testIndex, int totalTests, IJobLog log);
    void OnAfterGenerate(Problem problem, Config config, int testIndex, int totalTests, string input, IJobLog log);
    void OnBeforeRunSolution(Problem problem, Config config, string inputPath, int testIndex, IJobLog log);
    void OnAfterRunSolution(Problem problem, Config config, string inputPath, int testIndex, RunResult result, IJobLog log);
    void OnBeforeRunChecker(Problem problem, string checkerId, string inputPath, string outputPath, string answerPath, IJobLog log);
    void OnAfterRunChecker(Problem problem, string checkerId, string inputPath, string outputPath, string answerPath, bool accepted, string message, IJobLog log);
    void OnAuditStart(Problem problem, Config config, int testCount, IJobLog log);
    void OnAuditComplete(Problem problem, IOjGenAuditReport report, IJobLog log);
    void OnBeforePackage(string testDirectory, string zipPath, IJobLog log);
    void OnAfterPackage(string testDirectory, string zipPath, bool success, IJobLog log);
    void OnShutdown(IJobLog log);
}
```

### IOjGenAuditReport / IOjGenAuditItem — Audit Report Interfaces
```csharp
public interface IOjGenAuditReport
{
    bool Cancelled { get; }
    bool HasErrors { get; }
    int WarningCount { get; }
    int TotalCount { get; }
    int PassedCount { get; }
    int FailedCount { get; }
    IReadOnlyList<IOjGenAuditItem> Items { get; }
}

public interface IOjGenAuditItem
{
    string Level { get; } // "Pass", "Info", "Warning", "Error"
    string Title { get; }
    string Detail { get; }
}
```
Use case: Logging, metrics collection, CI integration, custom notifications.

## Accessing Registered Extensions

Extensions are registered in `PluginExtensionRegistry` and can be accessed:

```csharp
// Get all validators for a problem
var validators = PluginExtensionRegistry.GetValidators()
    .Where(v => v.ApplicableProblemIds.Count == 0 || v.ApplicableProblemIds.Contains(problem.Id));

// Get all checkers for a problem
var checkers = PluginExtensionRegistry.GetCheckers()
    .Where(c => c.ApplicableProblemIds.Count == 0 || c.ApplicableProblemIds.Contains(problem.Id));

// Get exporter by format name
var exporter = PluginExtensionRegistry.GetExporters()
    .FirstOrDefault(e => string.Equals(e.FormatName, "Custom Format", StringComparison.OrdinalIgnoreCase));

// Get solution runner for file extension
var runner = PluginExtensionRegistry.GetSolutionRunners()
    .FirstOrDefault(r => r.HandledExtensions.Contains(extension));

// Get event hooks
var hooks = PluginExtensionRegistry.GetEventHooks();
```

## Building and Testing a Plugin

1. Create a class library: `dotnet new classlib -n MyPlugin -f net10.0`
2. Add reference to OjGen.Core.dll (copy from release or build from source)
3. Implement your plugin class
4. Build: `dotnet build -c Release`
5. Test: `ojgen --plugin-check bin/Release/net10.0/MyPlugin.dll`

## Dependency Management

- Keep plugin dependencies alongside the plugin assembly
- The loader resolves dependencies from the plugin directory or manifest
- `OjGen.Core` is resolved from the host (same version as OjGen)

## API Versioning

- `ProblemPluginSdk.ApiVersion` increments only for breaking changes to public interfaces
- Plugins declare their `ApiVersion` via `IOjGenProblemPluginMetadata`
- Version 2 adds: `IOjGenPlugin`, `IOjGenValidator`, `IOjGenChecker`, `IOjGenExporter`, `IOjGenSolutionRunner`, `IOjGenEventHook`
- Plugins without metadata load in compatibility mode (warning printed)
- Multiple major versions cannot be loaded simultaneously

## Security

**Plugins are arbitrary .NET code running with the current user's permissions.** Only load plugins you trust. The `--plugin-check` command validates compatibility, not safety.