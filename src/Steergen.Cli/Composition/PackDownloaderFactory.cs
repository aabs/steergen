using Steergen.Core.Packs;
using Steergen.Core.Validation;

namespace Steergen.Cli.Composition;

/// <summary>
/// Centralised factory for creating <see cref="PackDownloader"/> instances
/// with a properly configured <see cref="HttpClient"/> for GitHub archive downloads.
/// Provides shared helper methods for branch-ref diagnostic warnings.
/// </summary>
public static class PackDownloaderFactory
{
    private static readonly Lazy<HttpClient> SharedHttpClient = new(() =>
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Steergen/1.0");
        client.Timeout = TimeSpan.FromMinutes(5);
        return client;
    });

    /// <summary>
    /// Creates a <see cref="PackDownloader"/> using the shared <see cref="HttpClient"/>
    /// configured for GitHub archive downloads and the default cache base directory.
    /// </summary>
    public static PackDownloader Create()
    {
        return new PackDownloader(SharedHttpClient.Value, GetCacheBaseDirectory());
    }

    /// <summary>
    /// Creates a <see cref="PackDownloader"/> using the shared <see cref="HttpClient"/>
    /// configured for GitHub archive downloads and a custom cache base directory.
    /// </summary>
    public static PackDownloader Create(string cacheBaseDirectory)
    {
        return new PackDownloader(SharedHttpClient.Value, cacheBaseDirectory);
    }

    /// <summary>
    /// Returns the default local pack cache base directory:
    /// <c>{userProfileDirectory}/.steergen</c>.
    /// </summary>
    public static string GetCacheBaseDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".steergen");
    }

    /// <summary>
    /// Emits a diagnostic warning to stderr if the given ref is a branch ref
    /// (not an immutable SHA pin and not a likely tag). Returns the diagnostic
    /// if one was emitted, or null otherwise.
    /// </summary>
    /// <param name="refValue">The Git ref value (tag, branch, or SHA).</param>
    /// <param name="packType">The type of pack (Template or Rules) for message context.</param>
    /// <returns>The emitted diagnostic, or null if no warning was needed.</returns>
    public static Diagnostic? EmitBranchRefWarning(string? refValue, PackType packType)
    {
        if (refValue is null)
            return null;

        if (PackDownloader.IsImmutablePin(refValue))
            return null;

        if (IsLikelyTag(refValue))
            return null;

        var code = packType == PackType.Template ? "TP008" : "RP006";
        var context = packType == PackType.Template ? "template" : "rule";
        var message = $"Using branch ref '{refValue}'. Consider pinning to a commit SHA or tag for deterministic {context} resolution.";

        var diagnostic = new Diagnostic(code, message, DiagnosticSeverity.Warning);
        Console.Error.WriteLine($"[warning] {code}: {message}");
        return diagnostic;
    }

    /// <summary>
    /// Heuristic: a ref that starts with 'v' followed by a digit is likely a tag.
    /// This is used to suppress the pinning recommendation for tag-like refs.
    /// </summary>
    internal static bool IsLikelyTag(string refValue)
    {
        return refValue.Length > 1
            && refValue[0] == 'v'
            && char.IsDigit(refValue[1]);
    }
}
