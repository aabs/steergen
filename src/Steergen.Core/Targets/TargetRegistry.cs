namespace Steergen.Core.Targets;

public static class TargetRegistry
{
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
