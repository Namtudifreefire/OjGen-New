# OjGen plugin SDK

The plugin contract currently has API version `1`. A plugin assembly must reference `OjGen.Core`, implement `IOjGenProblemPlugin`, and return its generator types from `CreateProblems()`.

Declare compatibility with the optional metadata interface:

```csharp
public sealed class ExamplePlugin : IOjGenProblemPlugin, IOjGenProblemPluginMetadata
{
    public string Name => "Example plugin";
    public int ApiVersion => ProblemPluginSdk.ApiVersion;
    public IEnumerable<Problem> CreateProblems() => new[] { new ExampleProblem() };
}
```

The generator type must derive from `Problem`. Its `Id` must not collide with a built-in or another loaded plugin. Existing plugins without `IOjGenProblemPluginMetadata` still load in compatibility mode and print a warning.

Check a plugin before wiring it into a project:

```powershell
ojgen --plugin-check .\bin\Release\net10.0\ExamplePlugin.dll
```

The check loads the assembly and invokes its plugin factory to verify registration. Plugins are arbitrary .NET code and run with the current user's permissions; only check or load plugins you trust. This is compatibility validation, not a security sandbox.

The loader resolves `OjGen.Core` from the host and plugin dependencies from the plugin's directory or dependency manifest. Keep plugin dependencies alongside the plugin assembly. Increase `ProblemPluginSdk.ApiVersion` only when the public extension contract has a breaking change.
