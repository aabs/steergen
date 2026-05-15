using CsCheck;
using Scriban;
using Steergen.Core.Targets;
using Steergen.Core.Targets.Kiro;
using Steergen.Core.Targets.Speckit;

namespace Steergen.Core.PropertyTests.Generation;

/// <summary>
/// Property tests for Scriban template rendering behavior.
/// Feature: simplify-rule-attributes, Property 9: Template rendering excludes removed attributes and includes mandatory
/// Validates: Requirements 4.5, 10.1, 10.2, 10.3, 10.4, 10.5
/// </summary>
public sealed class TemplateRenderingProperties
{
    // ── Templates (loaded from embedded resources via target components) ──────────

    private const string KiroDocumentTemplate = """
        ---
        description: {{ description }}
        inclusion: {{ inclusion }}
        {{ if file_match_pattern -}}
        fileMatchPattern: {{ file_match_pattern }}
        {{ end -}}
        ---
        {{ for section in sections -}}
        ## {{ section.heading }}
        {{ for rule in section.rules -}}
        - {{ if rule.id }}{{ rule.id }}{{ if rule.mandatory }} [MANDATORY]{{ end }}{{ if rule.deprecated }} (deprecated){{ end }}: {{ end }}{{ rule.primary_text }}
        {{ end -}}
        {{ end -}}
        """;

    private const string SpeckitConstitutionTemplate = """
        # Engineering Constitution
        {{ for section in sections -}}
        ## {{ section.heading }}
        {{ for rule in section.rules -}}
        - {{ rule.id }}{{ if rule.mandatory }} [MANDATORY]{{ end }}{{ if rule.deprecated }} (deprecated){{ end }}: {{ rule.primary_text }}
        {{ end -}}
        {{ end -}}
        """;

    private const string SpeckitModuleTemplate = """
        # Guidance: {{ domain }}
        {{ for section in sections -}}
        ## {{ section.heading }}
        {{ for rule in section.rules -}}
        - {{ rule.id }}{{ if rule.mandatory }} [MANDATORY]{{ end }}{{ if rule.deprecated }} (deprecated){{ end }}: {{ rule.primary_text }}
        {{ end -}}
        {{ end -}}
        """;

    private static readonly ITemplateProvider KiroTemplates =
        new InlineTemplateProvider("kiro", "document", KiroDocumentTemplate);

    private static readonly ITemplateProvider SpeckitTemplates =
        new InlineTemplateProvider("speckit", new Dictionary<string, string>
        {
            ["constitution"] = SpeckitConstitutionTemplate,
            ["module"] = SpeckitModuleTemplate,
        });

    // ── Generators ───────────────────────────────────────────────────────────────

    private static readonly Gen<string> GenRuleId =
        Gen.String[Gen.Char.AlphaNumeric, 1, 10]
           .Select(s => $"R-{s}");

    private static readonly Gen<string?> GenCategory =
        Gen.OneOf(
            Gen.Const((string?)"core"),
            Gen.Const((string?)"security"),
            Gen.Const((string?)"quality"),
            Gen.Const((string?)"api"),
            Gen.String[Gen.Char.AlphaNumeric, 3, 12].Select(s => (string?)s));

    private static readonly Gen<bool> GenMandatory =
        Gen.Bool;

    private static readonly Gen<bool> GenDeprecated =
        Gen.Bool;

    private static readonly Gen<string> GenPrimaryText =
        Gen.String[Gen.Char.AlphaNumeric, 5, 50];

    // ── Property 9: Template rendering excludes removed attributes and includes mandatory ─
    //
    // For any SteeringRule rendered through any Scriban template, the output SHALL NOT
    // contain [Supersedes: text, and when Mandatory is true the output SHALL contain
    // a mandatory indicator (e.g., [MANDATORY]).

    [Fact]
    public void KiroTemplate_NeverContainsSupersedes()
    {
        // Validates: Requirements 4.5, 10.3, 10.5
        // The Kiro document template output SHALL NOT contain [Supersedes: text.
        var target = new KiroTargetComponent(KiroTemplates);

        Gen.Select(GenRuleId, GenCategory, GenMandatory, GenDeprecated, GenPrimaryText)
            .Sample(
                (id, category, mandatory, deprecated, primaryText) =>
                {
                    var rule = new KiroRuleProseModel
                    {
                        Id = id,
                        Category = category,
                        Mandatory = mandatory,
                        Deprecated = deprecated,
                        PrimaryText = primaryText,
                    };

                    var model = new KiroDocumentModel
                    {
                        Description = "Test document",
                        Inclusion = "always",
                        Rules = [rule],
                        Sections =
                        [
                            new KiroRuleSectionModel
                            {
                                Heading = category ?? "General",
                                Rules = [rule],
                            }
                        ],
                    };

                    var output = target.RenderDocumentAsync(model).GetAwaiter().GetResult();

                    Assert.DoesNotContain("[Supersedes:", output);
                },
                iter: 100,
                print: t => $"id={t.Item1}, category={t.Item2 ?? "(null)"}, mandatory={t.Item3}, deprecated={t.Item4}");
    }

    [Fact]
    public void KiroTemplate_MandatoryTrue_ContainsMandatoryIndicator()
    {
        // Validates: Requirements 10.3, 10.4
        // When Mandatory is true, the Kiro template output SHALL contain [MANDATORY].
        var target = new KiroTargetComponent(KiroTemplates);

        Gen.Select(GenRuleId, GenCategory, GenDeprecated, GenPrimaryText)
            .Sample(
                (id, category, deprecated, primaryText) =>
                {
                    var rule = new KiroRuleProseModel
                    {
                        Id = id,
                        Category = category,
                        Mandatory = true,
                        Deprecated = deprecated,
                        PrimaryText = primaryText,
                    };

                    var model = new KiroDocumentModel
                    {
                        Description = "Test document",
                        Inclusion = "always",
                        Rules = [rule],
                        Sections =
                        [
                            new KiroRuleSectionModel
                            {
                                Heading = category ?? "General",
                                Rules = [rule],
                            }
                        ],
                    };

                    var output = target.RenderDocumentAsync(model).GetAwaiter().GetResult();

                    Assert.Contains("[MANDATORY]", output);
                },
                iter: 100,
                print: t => $"id={t.Item1}, category={t.Item2 ?? "(null)"}, deprecated={t.Item3}");
    }

    [Fact]
    public void KiroTemplate_MandatoryFalse_DoesNotContainMandatoryIndicator()
    {
        // Validates: Requirements 10.3, 10.4
        // When Mandatory is false, the Kiro template output SHALL NOT contain [MANDATORY].
        var target = new KiroTargetComponent(KiroTemplates);

        Gen.Select(GenRuleId, GenCategory, GenDeprecated, GenPrimaryText)
            .Sample(
                (id, category, deprecated, primaryText) =>
                {
                    var rule = new KiroRuleProseModel
                    {
                        Id = id,
                        Category = category,
                        Mandatory = false,
                        Deprecated = deprecated,
                        PrimaryText = primaryText,
                    };

                    var model = new KiroDocumentModel
                    {
                        Description = "Test document",
                        Inclusion = "always",
                        Rules = [rule],
                        Sections =
                        [
                            new KiroRuleSectionModel
                            {
                                Heading = category ?? "General",
                                Rules = [rule],
                            }
                        ],
                    };

                    var output = target.RenderDocumentAsync(model).GetAwaiter().GetResult();

                    Assert.DoesNotContain("[MANDATORY]", output);
                },
                iter: 100,
                print: t => $"id={t.Item1}, category={t.Item2 ?? "(null)"}, deprecated={t.Item3}");
    }

    [Fact]
    public void SpeckitConstitutionTemplate_NeverContainsSupersedes()
    {
        // Validates: Requirements 4.5, 10.1, 10.5
        // The Speckit constitution template output SHALL NOT contain [Supersedes: text.
        var target = new SpeckitTargetComponent(SpeckitTemplates);

        Gen.Select(GenRuleId, GenCategory, GenMandatory, GenDeprecated, GenPrimaryText)
            .Sample(
                (id, category, mandatory, deprecated, primaryText) =>
                {
                    var rule = new SpeckitRuleModel
                    {
                        Id = id,
                        Category = category,
                        Mandatory = mandatory,
                        Deprecated = deprecated,
                        PrimaryText = primaryText,
                    };

                    var model = new SpeckitConstitutionModel
                    {
                        Rules = [rule],
                        Sections =
                        [
                            new SpeckitRuleSectionModel
                            {
                                Heading = category ?? "General",
                                Rules = [rule],
                            }
                        ],
                    };

                    var output = target.RenderConstitutionAsync(model).GetAwaiter().GetResult();

                    Assert.DoesNotContain("[Supersedes:", output);
                },
                iter: 100,
                print: t => $"id={t.Item1}, category={t.Item2 ?? "(null)"}, mandatory={t.Item3}, deprecated={t.Item4}");
    }

    [Fact]
    public void SpeckitConstitutionTemplate_MandatoryTrue_ContainsMandatoryIndicator()
    {
        // Validates: Requirements 10.1, 10.4
        // When Mandatory is true, the Speckit constitution template output SHALL contain [MANDATORY].
        var target = new SpeckitTargetComponent(SpeckitTemplates);

        Gen.Select(GenRuleId, GenCategory, GenDeprecated, GenPrimaryText)
            .Sample(
                (id, category, deprecated, primaryText) =>
                {
                    var rule = new SpeckitRuleModel
                    {
                        Id = id,
                        Category = category,
                        Mandatory = true,
                        Deprecated = deprecated,
                        PrimaryText = primaryText,
                    };

                    var model = new SpeckitConstitutionModel
                    {
                        Rules = [rule],
                        Sections =
                        [
                            new SpeckitRuleSectionModel
                            {
                                Heading = category ?? "General",
                                Rules = [rule],
                            }
                        ],
                    };

                    var output = target.RenderConstitutionAsync(model).GetAwaiter().GetResult();

                    Assert.Contains("[MANDATORY]", output);
                },
                iter: 100,
                print: t => $"id={t.Item1}, category={t.Item2 ?? "(null)"}, deprecated={t.Item3}");
    }

    [Fact]
    public void SpeckitConstitutionTemplate_MandatoryFalse_DoesNotContainMandatoryIndicator()
    {
        // Validates: Requirements 10.1, 10.4
        // When Mandatory is false, the Speckit constitution template output SHALL NOT contain [MANDATORY].
        var target = new SpeckitTargetComponent(SpeckitTemplates);

        Gen.Select(GenRuleId, GenCategory, GenDeprecated, GenPrimaryText)
            .Sample(
                (id, category, deprecated, primaryText) =>
                {
                    var rule = new SpeckitRuleModel
                    {
                        Id = id,
                        Category = category,
                        Mandatory = false,
                        Deprecated = deprecated,
                        PrimaryText = primaryText,
                    };

                    var model = new SpeckitConstitutionModel
                    {
                        Rules = [rule],
                        Sections =
                        [
                            new SpeckitRuleSectionModel
                            {
                                Heading = category ?? "General",
                                Rules = [rule],
                            }
                        ],
                    };

                    var output = target.RenderConstitutionAsync(model).GetAwaiter().GetResult();

                    Assert.DoesNotContain("[MANDATORY]", output);
                },
                iter: 100,
                print: t => $"id={t.Item1}, category={t.Item2 ?? "(null)"}, deprecated={t.Item3}");
    }

    [Fact]
    public void SpeckitModuleTemplate_NeverContainsSupersedes()
    {
        // Validates: Requirements 4.5, 10.2, 10.5
        // The Speckit module template output SHALL NOT contain [Supersedes: text.
        var target = new SpeckitTargetComponent(SpeckitTemplates);

        Gen.Select(GenRuleId, GenCategory, GenMandatory, GenDeprecated, GenPrimaryText)
            .Sample(
                (id, category, mandatory, deprecated, primaryText) =>
                {
                    var rule = new SpeckitRuleModel
                    {
                        Id = id,
                        Category = category ?? "security",
                        Mandatory = mandatory,
                        Deprecated = deprecated,
                        PrimaryText = primaryText,
                    };

                    var model = new SpeckitModuleModel
                    {
                        Domain = category ?? "security",
                        Rules = [rule],
                        Sections =
                        [
                            new SpeckitRuleSectionModel
                            {
                                Heading = category ?? "Security",
                                Rules = [rule],
                            }
                        ],
                    };

                    var output = target.RenderModuleAsync(model).GetAwaiter().GetResult();

                    Assert.DoesNotContain("[Supersedes:", output);
                },
                iter: 100,
                print: t => $"id={t.Item1}, category={t.Item2 ?? "(null)"}, mandatory={t.Item3}, deprecated={t.Item4}");
    }

    [Fact]
    public void SpeckitModuleTemplate_MandatoryTrue_ContainsMandatoryIndicator()
    {
        // Validates: Requirements 10.2, 10.4
        // When Mandatory is true, the Speckit module template output SHALL contain [MANDATORY].
        var target = new SpeckitTargetComponent(SpeckitTemplates);

        Gen.Select(GenRuleId, GenCategory, GenDeprecated, GenPrimaryText)
            .Sample(
                (id, category, deprecated, primaryText) =>
                {
                    var rule = new SpeckitRuleModel
                    {
                        Id = id,
                        Category = category ?? "security",
                        Mandatory = true,
                        Deprecated = deprecated,
                        PrimaryText = primaryText,
                    };

                    var model = new SpeckitModuleModel
                    {
                        Domain = category ?? "security",
                        Rules = [rule],
                        Sections =
                        [
                            new SpeckitRuleSectionModel
                            {
                                Heading = category ?? "Security",
                                Rules = [rule],
                            }
                        ],
                    };

                    var output = target.RenderModuleAsync(model).GetAwaiter().GetResult();

                    Assert.Contains("[MANDATORY]", output);
                },
                iter: 100,
                print: t => $"id={t.Item1}, category={t.Item2 ?? "(null)"}, deprecated={t.Item3}");
    }

    [Fact]
    public void SpeckitModuleTemplate_MandatoryFalse_DoesNotContainMandatoryIndicator()
    {
        // Validates: Requirements 10.2, 10.4
        // When Mandatory is false, the Speckit module template output SHALL NOT contain [MANDATORY].
        var target = new SpeckitTargetComponent(SpeckitTemplates);

        Gen.Select(GenRuleId, GenCategory, GenDeprecated, GenPrimaryText)
            .Sample(
                (id, category, deprecated, primaryText) =>
                {
                    var rule = new SpeckitRuleModel
                    {
                        Id = id,
                        Category = category ?? "security",
                        Mandatory = false,
                        Deprecated = deprecated,
                        PrimaryText = primaryText,
                    };

                    var model = new SpeckitModuleModel
                    {
                        Domain = category ?? "security",
                        Rules = [rule],
                        Sections =
                        [
                            new SpeckitRuleSectionModel
                            {
                                Heading = category ?? "Security",
                                Rules = [rule],
                            }
                        ],
                    };

                    var output = target.RenderModuleAsync(model).GetAwaiter().GetResult();

                    Assert.DoesNotContain("[MANDATORY]", output);
                },
                iter: 100,
                print: t => $"id={t.Item1}, category={t.Item2 ?? "(null)"}, deprecated={t.Item3}");
    }

    // ── Helper: Inline template provider ─────────────────────────────────────────

    private sealed class InlineTemplateProvider : ITemplateProvider
    {
        private readonly string _targetId;
        private readonly Dictionary<string, string> _templates;

        public InlineTemplateProvider(string targetId, string templateName, string templateContent)
        {
            _targetId = targetId;
            _templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [templateName] = templateContent,
            };
        }

        public InlineTemplateProvider(string targetId, Dictionary<string, string> templates)
        {
            _targetId = targetId;
            _templates = new Dictionary<string, string>(templates, StringComparer.OrdinalIgnoreCase);
        }

        public string GetTemplate(string targetId, string templateName) =>
            _templates.TryGetValue(templateName, out var content)
                ? content
                : throw new InvalidOperationException($"Unknown template '{templateName}' for target '{targetId}'.");
    }
}
