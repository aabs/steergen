# Additive Target Extension Guide

specgen supports additive target registration without plugins or runtime assembly loading.
New targets are C# classes that implement `ITargetComponent` and are registered at startup
before the generation pipeline runs.

## Quick-Start

### 1. Implement `ITargetComponent`

```csharp
using Steergen.Core.Model;
using Steergen.Core.Targets;

public sealed class MyCustomTarget : ITargetComponent
{
    private static readonly TargetDescriptor Descriptor =
        new("my-target", "My Target", "Generates custom output from the steering model.");

    public string TargetId => "my-target";
    TargetDescriptor ITargetComponent.Descriptor => Descriptor;

    public Task GenerateWithPlanAsync(
        ResolvedSteeringModel model,
        TargetConfiguration config,
        WritePlan writePlan,
        CancellationToken cancellationToken)
    {
        var outputPath = config.OutputPath
            ?? throw new InvalidOperationException("my-target requires OutputPath.");

        Directory.CreateDirectory(outputPath);

        foreach (var file in writePlan.Files)
        {
            var destination = Path.Combine(outputPath, Path.GetFileName(file.Path));
            File.WriteAllText(destination, "custom output");
        }

        return Task.CompletedTask;
    }
}
```

### 2. Register before generating

Call `TargetRegistry.Register` **before** any generation pipeline invocation:

```csharp
TargetRegistry.Register(new MyCustomTarget());
```

Optionally attach `TargetRegistrationMetadata` to describe the target to tooling:

```csharp
var component = new MyCustomTarget();
var metadata = new TargetRegistrationMetadata
{
    TargetId    = component.TargetId,
    DisplayName = "My Target",
    Description = "Generates custom output from the steering model.",
    AuthorName  = "Acme Corp",
    Version     = "1.0.0",
    IsBuiltIn   = false,
};
TargetRegistry.Register(component);
```

### 3. Add a `TargetConfiguration` entry

In your `steergen.config.yaml` (or programmatic config), add an entry referencing your target ID:

```yaml
targets:
  - id: my-target
    enabled: true
    outputPath: .steergen/my-target
```

## Contract

| Constraint | Detail |
|---|---|
| `TargetId` | Unique, lowercase-kebab, stable across versions |
| `OutputPath` | Must be honoured; create the directory before writing |
| `Deprecated` rules | Filter rules with `r.Deprecated == true` unless the target intentionally includes them |
| Thread safety | `GenerateWithPlanAsync` may be called concurrently for distinct configs; do not share mutable state |
| Exceptions | Throw `TargetGenerationException` (from `Steergen.Core.Generation`) for recoverable generation errors |

## Compatibility guarantee

Registering an additive target has **no effect** on existing built-in target outputs.
Each `ITargetComponent` writes only to its own `config.OutputPath` directory.
The `TargetRegistry` rejects duplicate IDs so accidental shadowing of built-ins is caught at startup.

## Reference implementation

See `src/Steergen.Core/Targets/Fixtures/FixtureTargetComponent.cs` for a minimal working example.
