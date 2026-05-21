using CsCheck;
using Steergen.Core.Packs;

namespace Steergen.Core.PropertyTests.Packs;

/// <summary>
/// Property tests for SHA pinning detection in <see cref="PackDownloader"/>.
/// Feature: custom-template-packs, Property 4: SHA Pinning Detection
/// Validates: Requirements 3.6, 10.7
/// </summary>
public sealed class ShaDetectionProperties
{
    // ── Generators ───────────────────────────────────────────────────────────────

    private static readonly Gen<char> GenLowercaseHexChar =
        Gen.OneOf(
            Gen.Char['0', '9'],
            Gen.Char['a', 'f']);

    private static readonly Gen<string> GenValid40CharHex =
        Gen.String[GenLowercaseHexChar, 40, 40];

    private static readonly Gen<char> GenUppercaseHexChar =
        Gen.Char['A', 'F'];

    private static readonly Gen<char> GenNonHexChar =
        Gen.OneOf(
            Gen.Char['g', 'z'],
            Gen.Char['G', 'Z'],
            Gen.Char['!', '/']);

    private static readonly Gen<int> GenWrongLength =
        Gen.OneOf(
            Gen.Int[0, 39],
            Gen.Int[41, 100]);

    // ── Property 4: SHA Pinning Detection ────────────────────────────────────────

    [Fact]
    public void IsImmutablePin_ReturnsTrue_ForValid40CharLowercaseHex()
    {
        // **Validates: Requirements 3.6, 10.7**
        // For any string that is exactly 40 lowercase hex characters,
        // IsImmutablePin must return true.
        GenValid40CharHex
            .Sample(
                sha =>
                {
                    Assert.True(PackDownloader.IsImmutablePin(sha),
                        $"Expected true for valid SHA: '{sha}'");
                },
                iter: 200,
                print: sha => $"sha='{sha}'");
    }

    [Fact]
    public void IsImmutablePin_ReturnsFalse_ForStringsWithUppercaseHexChars()
    {
        // **Validates: Requirements 3.6, 10.7**
        // For any 40-char string containing at least one uppercase hex char,
        // IsImmutablePin must return false.
        Gen.Select(
            Gen.Int[0, 39],
            GenUppercaseHexChar,
            GenValid40CharHex)
           .Sample(
                (pos, upperChar, baseSha) =>
                {
                    var chars = baseSha.ToCharArray();
                    chars[pos] = upperChar;
                    var input = new string(chars);

                    Assert.False(PackDownloader.IsImmutablePin(input),
                        $"Expected false for SHA with uppercase at pos {pos}: '{input}'");
                },
                iter: 200,
                print: t => $"pos={t.Item1}, upperChar='{t.Item2}', input='{new string(t.Item3.ToCharArray().Select((c, i) => i == t.Item1 ? t.Item2 : c).ToArray())}'");
    }

    [Fact]
    public void IsImmutablePin_ReturnsFalse_ForWrongLengthStrings()
    {
        // **Validates: Requirements 3.6, 10.7**
        // For any string of lowercase hex chars that is NOT exactly 40 chars long,
        // IsImmutablePin must return false.
        GenWrongLength
            .SelectMany(len =>
                Gen.String[GenLowercaseHexChar, len, len])
            .Sample(
                input =>
                {
                    Assert.False(PackDownloader.IsImmutablePin(input),
                        $"Expected false for wrong-length hex string (len={input.Length}): '{input}'");
                },
                iter: 200,
                print: input => $"len={input.Length}, input='{(input.Length > 50 ? input[..50] + "..." : input)}'");
    }

    [Fact]
    public void IsImmutablePin_ReturnsFalse_ForStringsWithNonHexChars()
    {
        // **Validates: Requirements 3.6, 10.7**
        // For any 40-char string containing at least one non-hex character,
        // IsImmutablePin must return false.
        Gen.Select(
            Gen.Int[0, 39],
            GenNonHexChar,
            GenValid40CharHex)
           .Sample(
                (pos, nonHexChar, baseSha) =>
                {
                    var chars = baseSha.ToCharArray();
                    chars[pos] = nonHexChar;
                    var input = new string(chars);

                    Assert.False(PackDownloader.IsImmutablePin(input),
                        $"Expected false for SHA with non-hex char at pos {pos}: '{input}'");
                },
                iter: 200,
                print: t => $"pos={t.Item1}, nonHexChar='{t.Item2}'");
    }

    [Fact]
    public void IsImmutablePin_ReturnsFalse_ForNull()
    {
        // **Validates: Requirements 3.6, 10.7**
        // Null input must always return false.
        Assert.False(PackDownloader.IsImmutablePin(null));
    }

    [Fact]
    public void IsImmutablePin_ReturnsFalse_ForRandomStrings()
    {
        // **Validates: Requirements 3.6, 10.7**
        // For random strings of various lengths, IsImmutablePin returns true
        // if and only if the string is exactly 40 lowercase hex chars.
        Gen.String[Gen.Char.AlphaNumeric, 0, 80]
            .Sample(
                input =>
                {
                    var expected = input.Length == 40
                        && input.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

                    Assert.Equal(expected, PackDownloader.IsImmutablePin(input));
                },
                iter: 500,
                print: input => $"len={input.Length}, input='{(input.Length > 50 ? input[..50] + "..." : input)}'");
    }
}
