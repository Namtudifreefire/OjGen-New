# OjGen generator plugins

Plugins extend OjGen with custom input generators, validators, checkers, exporters, solution runners, and event hooks. The current extension API is `ProblemPluginSdk.ApiVersion == 2`.

## Create a plugin

1. Create a class library targeting `net10.0` and reference `OjGen.Core.dll` from the matching OjGen release.
2. Add a public class with a public parameterless constructor that implements `IOjGenPlugin` (or just `IOjGenProblemPlugin` for basic generators).
3. Implement the extension points you need:
   - `CreateProblems()` — input generators (required)
   - `CreateValidators()` — custom input validators (optional)
   - `CreateCheckers()` — custom output checkers (optional)
   - `CreateExporters()` — custom test package exporters (optional)
   - `CreateSolutionRunners()` — custom solution runners for other languages (optional)
   - `CreateEventHooks()` — lifecycle event hooks (optional)
4. Build the project and copy the plugin DLL and any private dependencies together.

### Basic generator plugin

```csharp
using OjGen.Core;

public sealed class ExamplePlugin : IOjGenProblemPlugin, IOjGenProblemPluginMetadata
{
    public string Name => "Example";
    public int ApiVersion => ProblemPluginSdk.ApiVersion;
    public string Version => "1.0.0";
    public string Author => "Your Name";
    public string Description => "Example problem generator";
    public string Url => "https://example.com";
    public string MinOjGenVersion => "3.3.0";

    public IEnumerable<Problem> CreateProblems()
    {
        yield return new ExampleProblem();
    }
}
```

### Full-featured plugin with all extension points

```csharp
using OjGen.Core;

public sealed class FullPlugin : IOjGenPlugin
{
    public string Name => "Full Plugin";
    public int ApiVersion => ProblemPluginSdk.ApiVersion;
    public string Version => "1.0.0";
    public string Author => "Your Name";
    public string Description => "Complete plugin with all extension points";
    public string Url => "https://example.com";
    public string MinOjGenVersion => "3.3.0";

    public IEnumerable<Problem> CreateProblems()
    {
        yield return new MyCustomProblem();
    }

    public IEnumerable<IOjGenValidator>? CreateValidators()
    {
        yield return new MyCustomValidator();
    }

    public IEnumerable<IOjGenChecker>? CreateCheckers()
    {
        yield return new MyCustomChecker();
    }

    public IEnumerable<IOjGenExporter>? CreateExporters()
    {
        yield return new MyCustomExporter();
    }

    public IEnumerable<IOjGenSolutionRunner>? CreateSolutionRunners()
    {
        yield return new MyCustomRunner();
    }

    public IEnumerable<IOjGenEventHook>? CreateEventHooks()
    {
        yield return new MyEventHook();
    }
}
```

## Load it

- **GUI:** Place the DLL in a `plugins` folder beside OjGen, or in `%AppData%\OjGen\plugins`, then restart the app.
- **CLI:** Pass `--plugin <DLL-or-folder>`. If omitted, OjGen checks a `plugins` folder beside the CLI executable.
- **Check compatibility:** Run `ojgen --plugin-check <DLL-or-folder>` before using a plugin. This loads the assembly and invokes the plugin factory; it checks compatibility but does not sandbox plugin code.

OjGen reports loading errors at startup. Plugins execute inside the OjGen process with the current user's permissions; only load DLLs you trust.

See [the extended SDK guide](docs/PLUGIN_SDK.md) for API versioning, dependency details, and interface references.