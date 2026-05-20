using CsCheck;
using Steergen.Core.Targets;

namespace Steergen.Core.PropertyTests.Packs;

/// <summary>
/// Property tests for template override precedence with target scoping.
///
/// Property 1: Template Override Precedence with Target Scoping
/// For any target ID and template name, and any combination of template availability
/// across the three layers (local override, cached GitHub pack, built-in embedded),
/// the TemplateResolver SHALL return the content from the highest-precedence layer
/// that contains the template for that target, where precedence is:
/// local override > cached GitHub pack > built-in embedded.
///
/// Additionally, for any template pack that declares a targets list, the resolver
/// SHALL only consult that pack's templates for the declared target IDs and fall
/// through to the next layer for undeclared targets.
///
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 15.1, 15.2, 15.3, 15.4**
/// </summary>
public sealed class TemplateResolverProperties : IDisposable
{
    private readonly string _testRoot;

    public TemplateResolverProperties()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "TemplateResolverProps_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    // ── Generators ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates valid target IDs (alphanumeric, lowercase, 2-10 chars).
    /// </summary>
    private static readonly Gen<string> GenTargetId =
        Gen.String[Gen.Char['a', 'z'], 2, 10];

    /// <summary>
    /// Generates valid template names (alphanumeric, lowercase, 3-12 chars).
    /// </summary>
    private static readonly Gen<string> GenTemplateName =
        Gen.String[Gen.Char['a', 'z'], 3, 12];

    /// <summary>
    /// Generates unique template content strings for each layer.
    /// </summary>
    private static Gen<string> GenContent(string layerPrefix) =>
        Gen.String[Gen.Char.AlphaNumeric, 5, 20]
           .Select(s => $"{layerPrefix}:{s}");

    /// <summary>
    /// Generates a boolean indicating whether a template is available at a given layer.
    /// </summary>
    private static readonly Gen<bool> GenAvailability = Gen.Bool;

    /// <summary>
    /// Generates a set of declared target IDs (1-4 targets).
    /// </summary>
    private static readonly Gen<HashSet<string>> GenDeclaredTargets =
        GenTargetId.Array[1, 4]
            .Select(arr => new HashSet<string>(arr, StringComparer.Ordinal));

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private string CreateLayerDir(string layerName)
    {
        var dir = Path.Combine(_testRoot, layerName + "_" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteTemplate(string layerPath, string targetId, string templateName, string content)
    {
        var dir = Path.Combine(layerPath, targetId);
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"{templateName}.scriban");
        File.WriteAllText(filePath, content);
    }

    // ── Property 1a: Resolver returns highest-precedence available layer ─────────

    [Fact]
    public void GetTemplate_ReturnsHighestPrecedenceLayer_ForAllCombinations()
    {
        // **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
        //
        // For any (targetId, templateName) with random availability across layers,
        // the resolver returns content from the highest-precedence available layer.
        Gen.Select(
            GenTargetId,
            GenTemplateName,
            GenAvailability, // local available?
            GenAvailability, // cached available?
            GenContent("local"),
            GenContent("cached"),
            GenContent("embedded"))
        .Sample(
            (targetId, templateName, localAvail, cachedAvail, localContent, cachedContent, embeddedContent) =>
            {
                var localDir = CreateLayerDir("local");
                var cachedDir = CreateLayerDir("cached");

                if (localAvail)
                    WriteTemplate(localDir, targetId, templateName, localContent);
                if (cachedAvail)
                    WriteTemplate(cachedDir, targetId, templateName, cachedContent);

                var embeddedProvider = new FakeEmbeddedProvider(targetId, templateName, embeddedContent);

                var resolver = new TemplateResolver(
                    localOverridePath: localDir,
                    cachedPackPath: cachedDir,
                    embeddedProvider: embeddedProvider,
                    declaredTargets: null); // No target scoping — applies to all

                var result = resolver.GetTemplate(targetId, templateName);

                // Assert highest-precedence layer wins
                if (localAvail)
                    Assert.Equal(localContent, result);
                else if (cachedAvail)
                    Assert.Equal(cachedContent, result);
                else
                    Assert.Equal(embeddedContent, result);
            },
            iter: 200,
            print: t => $"target={t.Item1}, template={t.Item2}, local={t.Item3}, cached={t.Item4}");
    }

    // ── Property 1b: Local override takes precedence over cached ─────────────────

    [Fact]
    public void GetTemplate_LocalOverrideTakesPrecedenceOverCached_WhenBothAvailable()
    {
        // **Validates: Requirements 1.1, 1.4**
        Gen.Select(GenTargetId, GenTemplateName, GenContent("local"), GenContent("cached"))
            .Sample(
                (targetId, templateName, localContent, cachedContent) =>
                {
                    var localDir = CreateLayerDir("local");
                    var cachedDir = CreateLayerDir("cached");

                    WriteTemplate(localDir, targetId, templateName, localContent);
                    WriteTemplate(cachedDir, targetId, templateName, cachedContent);

                    var embeddedProvider = new FakeEmbeddedProvider(targetId, templateName, "embedded_fallback");

                    var resolver = new TemplateResolver(
                        localOverridePath: localDir,
                        cachedPackPath: cachedDir,
                        embeddedProvider: embeddedProvider,
                        declaredTargets: null);

                    var result = resolver.GetTemplate(targetId, templateName);

                    Assert.Equal(localContent, result);
                },
                iter: 100,
                print: t => $"target={t.Item1}, template={t.Item2}");
    }

    // ── Property 1c: Cached takes precedence over embedded ───────────────────────

    [Fact]
    public void GetTemplate_CachedTakesPrecedenceOverEmbedded_WhenLocalMissing()
    {
        // **Validates: Requirements 1.2, 1.3, 1.4**
        Gen.Select(GenTargetId, GenTemplateName, GenContent("cached"), GenContent("embedded"))
            .Sample(
                (targetId, templateName, cachedContent, embeddedContent) =>
                {
                    var localDir = CreateLayerDir("local"); // empty — no templates
                    var cachedDir = CreateLayerDir("cached");

                    WriteTemplate(cachedDir, targetId, templateName, cachedContent);

                    var embeddedProvider = new FakeEmbeddedProvider(targetId, templateName, embeddedContent);

                    var resolver = new TemplateResolver(
                        localOverridePath: localDir,
                        cachedPackPath: cachedDir,
                        embeddedProvider: embeddedProvider,
                        declaredTargets: null);

                    var result = resolver.GetTemplate(targetId, templateName);

                    Assert.Equal(cachedContent, result);
                },
                iter: 100,
                print: t => $"target={t.Item1}, template={t.Item2}");
    }

    // ── Property 1d: Falls back to embedded when no overrides exist ──────────────

    [Fact]
    public void GetTemplate_FallsBackToEmbedded_WhenNoOverridesExist()
    {
        // **Validates: Requirements 1.3, 1.4**
        Gen.Select(GenTargetId, GenTemplateName, GenContent("embedded"))
            .Sample(
                (targetId, templateName, embeddedContent) =>
                {
                    var localDir = CreateLayerDir("local"); // empty
                    var cachedDir = CreateLayerDir("cached"); // empty

                    var embeddedProvider = new FakeEmbeddedProvider(targetId, templateName, embeddedContent);

                    var resolver = new TemplateResolver(
                        localOverridePath: localDir,
                        cachedPackPath: cachedDir,
                        embeddedProvider: embeddedProvider,
                        declaredTargets: null);

                    var result = resolver.GetTemplate(targetId, templateName);

                    Assert.Equal(embeddedContent, result);
                },
                iter: 100,
                print: t => $"target={t.Item1}, template={t.Item2}");
    }

    // ── Property 1e: Target-scoped packs only consulted for declared targets ─────

    [Fact]
    public void GetTemplate_TargetScopedPack_OnlyConsultedForDeclaredTargets()
    {
        // **Validates: Requirements 15.1, 15.2, 15.3, 15.4**
        //
        // When declaredTargets is non-null, the resolver only serves templates
        // from local/cached layers for those declared targets. Undeclared targets
        // fall through to the embedded provider even if templates exist in local/cached.
        Gen.Select(
            GenDeclaredTargets,
            GenTargetId, // target to query (may or may not be in declared set)
            GenTemplateName,
            GenContent("local"),
            GenContent("cached"),
            GenContent("embedded"))
        .Sample(
            (declaredTargets, queryTarget, templateName, localContent, cachedContent, embeddedContent) =>
            {
                var localDir = CreateLayerDir("local");
                var cachedDir = CreateLayerDir("cached");

                // Write templates for the query target in both local and cached layers
                WriteTemplate(localDir, queryTarget, templateName, localContent);
                WriteTemplate(cachedDir, queryTarget, templateName, cachedContent);

                var embeddedProvider = new FakeEmbeddedProvider(queryTarget, templateName, embeddedContent);

                var resolver = new TemplateResolver(
                    localOverridePath: localDir,
                    cachedPackPath: cachedDir,
                    embeddedProvider: embeddedProvider,
                    declaredTargets: declaredTargets);

                var result = resolver.GetTemplate(queryTarget, templateName);

                if (declaredTargets.Contains(queryTarget))
                {
                    // Target IS declared — local override should win
                    Assert.Equal(localContent, result);
                }
                else
                {
                    // Target is NOT declared — should fall through to embedded
                    Assert.Equal(embeddedContent, result);
                }
            },
            iter: 200,
            print: t => $"declared=[{string.Join(",", t.Item1)}], query={t.Item2}, template={t.Item3}");
    }

    // ── Property 1f: Null declaredTargets means all targets are served ────────────

    [Fact]
    public void GetTemplate_NullDeclaredTargets_ServesAllTargets()
    {
        // **Validates: Requirements 15.3**
        //
        // When declaredTargets is null, the resolver serves templates for all targets
        // (backward-compatible behaviour).
        Gen.Select(GenTargetId, GenTemplateName, GenContent("local"), GenContent("embedded"))
            .Sample(
                (targetId, templateName, localContent, embeddedContent) =>
                {
                    var localDir = CreateLayerDir("local");
                    WriteTemplate(localDir, targetId, templateName, localContent);

                    var embeddedProvider = new FakeEmbeddedProvider(targetId, templateName, embeddedContent);

                    var resolver = new TemplateResolver(
                        localOverridePath: localDir,
                        cachedPackPath: null,
                        embeddedProvider: embeddedProvider,
                        declaredTargets: null); // null = all targets

                    var result = resolver.GetTemplate(targetId, templateName);

                    // Should use local override for any target
                    Assert.Equal(localContent, result);
                },
                iter: 100,
                print: t => $"target={t.Item1}, template={t.Item2}");
    }

    // ── Property 1g: GetTemplateSource reports correct source layer ───────────────

    [Fact]
    public void GetTemplateSource_ReportsCorrectLayer_ForAllCombinations()
    {
        // **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
        Gen.Select(
            GenTargetId,
            GenTemplateName,
            GenAvailability,
            GenAvailability,
            GenContent("local"),
            GenContent("cached"))
        .Sample(
            (targetId, templateName, localAvail, cachedAvail, localContent, cachedContent) =>
            {
                var localDir = CreateLayerDir("local");
                var cachedDir = CreateLayerDir("cached");

                if (localAvail)
                    WriteTemplate(localDir, targetId, templateName, localContent);
                if (cachedAvail)
                    WriteTemplate(cachedDir, targetId, templateName, cachedContent);

                var embeddedProvider = new FakeEmbeddedProvider(targetId, templateName, "embedded");

                var resolver = new TemplateResolver(
                    localOverridePath: localDir,
                    cachedPackPath: cachedDir,
                    embeddedProvider: embeddedProvider,
                    declaredTargets: null);

                var source = resolver.GetTemplateSource(targetId, templateName);

                if (localAvail)
                    Assert.Equal(TemplateSource.LocalOverride, source);
                else if (cachedAvail)
                    Assert.Equal(TemplateSource.CachedGitHubPack, source);
                else
                    Assert.Equal(TemplateSource.BuiltInEmbedded, source);
            },
            iter: 200,
            print: t => $"target={t.Item1}, template={t.Item2}, local={t.Item3}, cached={t.Item4}");
    }

    // ── Property 1h: ProvidesForTarget respects declaredTargets ───────────────────

    [Fact]
    public void ProvidesForTarget_RespectsTargetDeclaration()
    {
        // **Validates: Requirements 15.1, 15.2**
        Gen.Select(GenDeclaredTargets, GenTargetId)
            .Sample(
                (declaredTargets, queryTarget) =>
                {
                    var embeddedProvider = new FakeEmbeddedProvider("any", "any", "content");

                    var resolver = new TemplateResolver(
                        localOverridePath: null,
                        cachedPackPath: null,
                        embeddedProvider: embeddedProvider,
                        declaredTargets: declaredTargets);

                    var provides = resolver.ProvidesForTarget(queryTarget);

                    Assert.Equal(declaredTargets.Contains(queryTarget), provides);
                },
                iter: 200,
                print: t => $"declared=[{string.Join(",", t.Item1)}], query={t.Item2}");
    }

    // ── Property 6: Template Resolution Determinism ─────────────────────────────

    // Property 6: Template Resolution Determinism
    // For any template resolver state (fixed local override path, cached pack path,
    // and embedded templates), calling GetTemplate with the same (targetId, templateName)
    // arguments SHALL always return the same content. Additionally, for any set of
    // template files in a pack directory, the enumeration order SHALL be deterministic
    // (ordinal string sort of relative paths).
    //
    // **Validates: Requirements 5.1, 5.4**

    [Fact]
    public void GetTemplate_IsDeterministic_SameArgsSameResult()
    {
        // **Validates: Requirements 5.1, 5.4**
        //
        // For any resolver state, calling GetTemplate twice with the same
        // (targetId, templateName) arguments returns identical results.
        Gen.Select(
            GenTargetId,
            GenTemplateName,
            GenAvailability,
            GenAvailability,
            GenContent("local"),
            GenContent("cached"),
            GenContent("embedded"),
            Gen.Bool)
        .Sample(
            (targetId, templateName, localAvail, cachedAvail, localContent, cachedContent, embeddedContent, useDeclared) =>
            {
                var localDir = CreateLayerDir("det_local");
                var cachedDir = CreateLayerDir("det_cached");

                if (localAvail)
                    WriteTemplate(localDir, targetId, templateName, localContent);
                if (cachedAvail)
                    WriteTemplate(cachedDir, targetId, templateName, cachedContent);

                // When useDeclared, create a declared targets set that includes the target
                HashSet<string>? targets = useDeclared
                    ? new HashSet<string>(StringComparer.Ordinal) { targetId }
                    : null;
                var embeddedProvider = new FakeEmbeddedProvider(targetId, templateName, embeddedContent);

                var resolver = new TemplateResolver(
                    localOverridePath: localDir,
                    cachedPackPath: cachedDir,
                    embeddedProvider: embeddedProvider,
                    declaredTargets: targets);

                // Call GetTemplate twice with the same arguments
                var result1 = resolver.GetTemplate(targetId, templateName);
                var result2 = resolver.GetTemplate(targetId, templateName);

                Assert.Equal(result1, result2);
            },
            iter: 200,
            print: t => $"target={t.Item1}, template={t.Item2}, local={t.Item3}, cached={t.Item4}, declaredTargets={t.Item8}");
    }

    [Fact]
    public void GetTemplate_IsDeterministic_AcrossMultipleResolverInstances()
    {
        // **Validates: Requirements 5.1, 5.4**
        //
        // For any fixed filesystem state, constructing two separate TemplateResolver
        // instances with the same configuration and calling GetTemplate returns
        // identical results — proving determinism is not instance-dependent.
        Gen.Select(
            GenTargetId,
            GenTemplateName,
            GenAvailability,
            GenAvailability,
            GenContent("local"),
            GenContent("cached"),
            GenContent("embedded"))
        .Sample(
            (targetId, templateName, localAvail, cachedAvail, localContent, cachedContent, embeddedContent) =>
            {
                var localDir = CreateLayerDir("det_inst_local");
                var cachedDir = CreateLayerDir("det_inst_cached");

                if (localAvail)
                    WriteTemplate(localDir, targetId, templateName, localContent);
                if (cachedAvail)
                    WriteTemplate(cachedDir, targetId, templateName, cachedContent);

                var embeddedProvider1 = new FakeEmbeddedProvider(targetId, templateName, embeddedContent);
                var embeddedProvider2 = new FakeEmbeddedProvider(targetId, templateName, embeddedContent);

                var resolver1 = new TemplateResolver(
                    localOverridePath: localDir,
                    cachedPackPath: cachedDir,
                    embeddedProvider: embeddedProvider1,
                    declaredTargets: null);

                var resolver2 = new TemplateResolver(
                    localOverridePath: localDir,
                    cachedPackPath: cachedDir,
                    embeddedProvider: embeddedProvider2,
                    declaredTargets: null);

                var result1 = resolver1.GetTemplate(targetId, templateName);
                var result2 = resolver2.GetTemplate(targetId, templateName);

                Assert.Equal(result1, result2);
            },
            iter: 200,
            print: t => $"target={t.Item1}, template={t.Item2}, local={t.Item3}, cached={t.Item4}");
    }

    [Fact]
    public void GetTemplate_IsDeterministic_WithTargetScoping()
    {
        // **Validates: Requirements 5.1, 5.4**
        //
        // For any resolver state with target scoping, calling GetTemplate multiple
        // times with the same arguments always returns the same content, regardless
        // of whether the target is in the declared set or not.
        Gen.Select(
            GenDeclaredTargets,
            GenTargetId,
            GenTemplateName,
            GenContent("local"),
            GenContent("cached"),
            GenContent("embedded"))
        .Sample(
            (declaredTargets, queryTarget, templateName, localContent, cachedContent, embeddedContent) =>
            {
                var localDir = CreateLayerDir("det_scope_local");
                var cachedDir = CreateLayerDir("det_scope_cached");

                WriteTemplate(localDir, queryTarget, templateName, localContent);
                WriteTemplate(cachedDir, queryTarget, templateName, cachedContent);

                var embeddedProvider = new FakeEmbeddedProvider(queryTarget, templateName, embeddedContent);

                var resolver = new TemplateResolver(
                    localOverridePath: localDir,
                    cachedPackPath: cachedDir,
                    embeddedProvider: embeddedProvider,
                    declaredTargets: declaredTargets);

                // Call three times to verify determinism
                var result1 = resolver.GetTemplate(queryTarget, templateName);
                var result2 = resolver.GetTemplate(queryTarget, templateName);
                var result3 = resolver.GetTemplate(queryTarget, templateName);

                Assert.Equal(result1, result2);
                Assert.Equal(result2, result3);
            },
            iter: 150,
            print: t => $"declared=[{string.Join(",", t.Item1)}], query={t.Item2}, template={t.Item3}");
    }

    [Fact]
    public void GetTemplateSource_IsDeterministic_SameArgsSameResult()
    {
        // **Validates: Requirements 5.1, 5.4**
        //
        // For any resolver state, calling GetTemplateSource twice with the same
        // arguments returns the same TemplateSource value.
        Gen.Select(
            GenTargetId,
            GenTemplateName,
            GenAvailability,
            GenAvailability,
            GenContent("local"),
            GenContent("cached"))
        .Sample(
            (targetId, templateName, localAvail, cachedAvail, localContent, cachedContent) =>
            {
                var localDir = CreateLayerDir("det_src_local");
                var cachedDir = CreateLayerDir("det_src_cached");

                if (localAvail)
                    WriteTemplate(localDir, targetId, templateName, localContent);
                if (cachedAvail)
                    WriteTemplate(cachedDir, targetId, templateName, cachedContent);

                var embeddedProvider = new FakeEmbeddedProvider(targetId, templateName, "embedded");

                var resolver = new TemplateResolver(
                    localOverridePath: localDir,
                    cachedPackPath: cachedDir,
                    embeddedProvider: embeddedProvider,
                    declaredTargets: null);

                var source1 = resolver.GetTemplateSource(targetId, templateName);
                var source2 = resolver.GetTemplateSource(targetId, templateName);

                Assert.Equal(source1, source2);
            },
            iter: 150,
            print: t => $"target={t.Item1}, template={t.Item2}, local={t.Item3}, cached={t.Item4}");
    }

    // ── Fake embedded provider ───────────────────────────────────────────────────

    /// <summary>
    /// A fake ITemplateProvider that returns a fixed content string for any request.
    /// Used to simulate the built-in embedded template layer.
    /// </summary>
    private sealed class FakeEmbeddedProvider : ITemplateProvider
    {
        private readonly string _targetId;
        private readonly string _templateName;
        private readonly string _content;

        public FakeEmbeddedProvider(string targetId, string templateName, string content)
        {
            _targetId = targetId;
            _templateName = templateName;
            _content = content;
        }

        public string GetTemplate(string targetId, string templateName)
        {
            // Always return the configured content regardless of target/template
            // This simulates the embedded provider always having a fallback
            return _content;
        }
    }
}
