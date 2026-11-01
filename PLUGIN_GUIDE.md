# OjGen generator plugins

Plugins add input generators that implement `OjGen.Core.Problem`.

## Create a plugin

1. Create a class library targeting `net10.0` and reference `OjGen.Core.dll` from the matching OjGen release.
2. Add a public class with a public parameterless constructor that implements `IOjGenProblemPlugin`.
3. Return one or more `Problem` implementations from `CreateProblems()`. Each problem ID must be unique and its `ValidateConfig`, `Generate`, and `ValidateInput` methods must enforce the input format and bounds.
4. Build the project and copy the plugin DLL and any private dependencies together.

```csharp
using OjGen.Core;

public sealed class ExamplePlugin : IOjGenProblemPlugin
{
    public string Name => "Example";

    public IEnumerable<Problem> CreateProblems()
    {
        yield return new ExampleProblem();
    }
}
```

## Load it

- **GUI:** Place the DLL in a `plugins` folder beside OjGen, or in `%AppData%\OjGen\plugins`, then restart the app.
- **CLI:** Pass `--plugin <DLL-or-folder>`. If omitted, OjGen checks a `plugins` folder beside the CLI executable.

OjGen reports loading errors at startup. Plugins execute inside the OjGen process with the current user's permissions; only load DLLs you trust.
