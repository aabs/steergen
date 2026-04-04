namespace Steergen.Core.Targets;

public interface ITemplateProvider
{
    string GetTemplate(string targetId, string templateName);
}
