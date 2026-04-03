using System.Reflection;
using Steergen.Core.Targets;

namespace Steergen.Templates;

/// <summary>
/// Loads Scriban template content from embedded resources in this assembly.
/// Resource naming: <c>Steergen.Templates.Scriban.{targetId}.{templateName}.scriban</c>
/// </summary>
public sealed class EmbeddedTemplateProvider : ITemplateProvider
{
    private static readonly Assembly ResourceAssembly =
        typeof(EmbeddedTemplateProvider).Assembly;

    public string GetTemplate(string targetId, string templateName)
    {
        var resourceName = $"Steergen.Templates.Scriban.{targetId}.{templateName}.scriban";
        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded template '{resourceName}' not found. " +
                $"Available: {string.Join(", ", ResourceAssembly.GetManifestResourceNames())}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
