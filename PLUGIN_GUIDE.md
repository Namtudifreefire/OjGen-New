# OjGen generator plugins

Plugins add input generators that implement `OjGen.Core.Problem`. The current extension API is `ProblemPluginSdk.ApiVersion == 1`.

## Create a plugin

1. Create a class library targeting `net10.0` and reference `OjGen.Core.dll` from the matching OjGen release.
2. Add a public class with a public parameterless constructor that implements `IOjGenProblemPlugin`.
3. Return one or more `Problem` implementations from `CreateProblems()`. Each problem ID must be unique and its `ValidateConfig`, `Generate`, and `ValidateInput` methods must enforce the input format and bounds.
4. Build the project and copy the plugin DLL and any private dependencies together.

```csharp
using OjGen.Core;

public sealed class ExamplePlugin : IOjGenProblemPlugin, IOjGenProblemPluginMetadata
{
    public string Name => "Example";
    public int ApiVersion => ProblemPluginSdk.ApiVersion;

    public IEnumerable<Problem> CreateProblems()
    {
        yield return new ExampleProblem();
    }
}
```

## Load it

- **GUI:** Place the DLL in a `plugins` folder beside OjGen, or in `%AppData%\OjGen\plugins`, then restart the app.
- **CLI:** Pass `--plugin <DLL-or-folder>`. If omitted, OjGen checks a `plugins` folder beside the CLI executable.
- **Check compatibility:** Run `ojgen --plugin-check <DLL-or-folder>` before using a plugin. This loads the assembly and invokes the plugin factory; it checks compatibility but does not sandbox plugin code.

OjGen reports loading errors at startup. Plugins execute inside the OjGen process with the current user's permissions; only load DLLs you trust.

See [the extended SDK guide](docs/PLUGIN_SDK.md) for API versioning and dependency details.
