using Steergen.Core.Packs;
using Steergen.Core.Validation;

namespace Steergen.Core.Targets;

public static class TargetRegistry
{
    /// <summary>Known built-in target IDs.</summary>
    public static class KnownTargets
    {
        public const string Speckit = "speckit";
        public const string Kiro = "kiro";
        public const string CopilotAgent = "copilot-agent";
        public const string KiroAgent = "kiro-agent";
    }

    private static readonly Dictionary<string, ITargetComponent> Components =
        new(StringComparer.Ordinal);

    private static readonly object Lock = new();

    public static void Register(ITargetComponent component)
    {
        lock (Lock)
        {
            if (Components.ContainsKey(component.TargetId))
                throw new InvalidOperationException(
                    $"A target with ID '{component.TargetId}' is already registered.");
            Components[component.TargetId] = component;
        }
    }

    /// <summary>
    /// Registers all built-in targets (Speckit, Kiro, CopilotAgent, KiroAgent). Safe to call once at startup.
    /// </summary>
    public static void RegisterBuiltins(ITemplateProvider templateProvider)
    {
        Register(new Speckit.SpeckitTargetComponent(templateProvider));
        Register(new Kiro.KiroTargetComponent(templateProvider));
        Register(new Agents.CopilotAgentTargetComponent(templateProvider));
        Register(new Agents.KiroAgentTargetComponent(templateProvider));
    }

    /// <summary>
    /// Registers pack-provided targets from a loaded template pack manifest.
    /// Only registers targets whose <c>defaultLayout</c> file exists within the pack directory.
    /// Emits TP009 diagnostic for targets with missing layout files.
    /// </summary>
    /// <returns>Diagnostics for any targets that could not be registered.</returns>
    public static IReadOnlyList<Diagnostic> RegisterPackTargets(
        PackManifest manifest,
        string packBasePath,
        ITemplateProvider templateProvider)
    {
        var diagnostics = new List<Diagnostic>();

        if (manifest.ProvidedTargets is null || manifest.ProvidedTargets.Count == 0)
            return diagnostics;

        lock (Lock)
        {
            foreach (var target in manifest.ProvidedTargets)
            {
                var layoutPath = Path.Combine(packBasePath, target.DefaultLayout);

                if (!File.Exists(layoutPath))
                {
                    diagnostics.Add(new Diagnostic(
                        "TP009",
                        $"Provided target '{target.TargetId}' declares defaultLayout '{target.DefaultLayout}' but the file does not exist in pack directory '{packBasePath}'.",
                        DiagnosticSeverity.Error));
                    continue;
                }

                if (Components.ContainsKey(target.TargetId))
                {
                    diagnostics.Add(new Diagnostic(
                        "TP011",
                        $"A target with ID '{target.TargetId}' is already registered. Cannot register pack-provided target from '{manifest.Name}'.",
                        DiagnosticSeverity.Error));
                    continue;
                }

                var component = new PackTargetComponent(
                    target.TargetId,
                    templateProvider,
                    layoutPath,
                    manifest.Name,
                    target.Description);

                Components[target.TargetId] = component;
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Returns true if the target is available (built-in or pack-provided).
    /// </summary>
    public static bool IsAvailable(string targetId)
    {
        lock (Lock)
        {
            return Components.ContainsKey(targetId);
        }
    }

    /// <summary>
    /// Returns all available targets: built-in + pack-provided.
    /// </summary>
    public static IReadOnlyList<TargetDescriptor> GetAvailableTargets()
    {
        lock (Lock)
        {
            return Components.Values
                .OrderBy(c => c.TargetId, StringComparer.Ordinal)
                .Select(c => c.Descriptor)
                .ToList();
        }
    }

    public static IReadOnlyList<ITargetComponent> GetAll()
    {
        lock (Lock)
        {
            return Components.Values
                .OrderBy(c => c.TargetId, StringComparer.Ordinal)
                .ToList();
        }
    }

    /// <summary>
    /// Returns the embedded resource name of the default layout YAML for a built-in target.
    /// Delegates to <see cref="Configuration.LayoutOverrideLoader.GetEmbeddedResourceName"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown for unknown target IDs.</exception>
    public static string GetDefaultLayoutResourceName(string targetId) =>
        Configuration.LayoutOverrideLoader.GetEmbeddedResourceName(targetId);

    /// <summary>
    /// Returns true if <paramref name="targetId"/> has a built-in default layout YAML.
    /// </summary>
    public static bool HasDefaultLayout(string targetId)
    {
        try
        {
            Configuration.LayoutOverrideLoader.GetEmbeddedResourceName(targetId);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether removing a pack would leave registered targets orphaned.
    /// Returns TP010 diagnostics for any targets still registered that were provided by the pack.
    /// </summary>
    public static IReadOnlyList<Diagnostic> ValidatePackRemoval(
        string packName,
        IReadOnlyList<string> registeredTargets)
    {
        var diagnostics = new List<Diagnostic>();

        lock (Lock)
        {
            foreach (var targetId in registeredTargets)
            {
                if (Components.TryGetValue(targetId, out var component) &&
                    component.Descriptor.Origin == TargetOrigin.PackProvided &&
                    string.Equals(component.Descriptor.PackName, packName, StringComparison.Ordinal))
                {
                    diagnostics.Add(new Diagnostic(
                        "TP010",
                        $"Target '{targetId}' is still registered but its providing pack '{packName}' is being removed. Remove the target first with 'steergen target remove {targetId}'.",
                        DiagnosticSeverity.Error));
                }
            }
        }

        return diagnostics;
    }

    internal static void Clear()
    {
        lock (Lock)
        {
            Components.Clear();
        }
    }
}
