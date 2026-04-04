using System.Diagnostics;

namespace Steergen.Cli.Diagnostics;

/// <summary>
/// Opt-in timing measurement reporter for SC-001/SC-005 success criteria.
/// Emits <c>[measure] {name}: {elapsed}ms</c> lines to stderr when enabled.
/// Disabled by default; enabled only when <c>--verbose</c> or <c>--debug</c> is active
/// (NFR-016: measurement routines MUST NOT execute during normal command operation).
/// </summary>
public sealed class MeasurementProtocolReporter
{
    private readonly bool _enabled;
    private readonly Stopwatch _totalTimer;

    public MeasurementProtocolReporter(bool enabled)
    {
        _enabled = enabled;
        _totalTimer = enabled ? Stopwatch.StartNew() : new Stopwatch();
    }

    /// <summary>
    /// Measures the elapsed time of <paramref name="work"/> and emits a <c>[measure]</c> line
    /// to stderr when this reporter is enabled.
    /// </summary>
    public async Task<T> MeasureAsync<T>(string name, Func<Task<T>> work)
    {
        if (!_enabled)
            return await work();

        var sw = Stopwatch.StartNew();
        var result = await work();
        sw.Stop();
        Emit(name, sw.Elapsed);
        return result;
    }

    /// <summary>
    /// Emits a <c>[measure] total</c> line summarising the elapsed time since construction.
    /// No-op when disabled.
    /// </summary>
    public void EmitTotal()
    {
        if (!_enabled)
            return;
        _totalTimer.Stop();
        Emit("total", _totalTimer.Elapsed);
    }

    private static void Emit(string name, TimeSpan elapsed)
        => Console.Error.WriteLine($"[measure] {name}: {elapsed.TotalMilliseconds:F1}ms");
}
