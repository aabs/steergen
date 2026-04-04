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

    public static IReadOnlyList<ITargetComponent> GetAll()
    {
        lock (Lock)
        {
            return Components.Values
                .OrderBy(c => c.TargetId, StringComparer.Ordinal)
                .ToList();
        }
    }

    internal static void Clear()
    {
        lock (Lock)
        {
            Components.Clear();
        }
    }
}
