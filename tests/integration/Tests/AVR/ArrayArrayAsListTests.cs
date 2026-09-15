using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/array-array-as-list (PyMCU#433): `import array` / `array.array(typecode)`,
/// read as the heap-bounded `list[T]` the compiler already has.
///
/// Four bugs stood between `import array` and adafruit_dht's shape working, three of them
/// silent wrong code: `array` was a hard ImportError; a `list[T]` parameter on a plain
/// (non-@inline) function was refused outright, on a claim (silent drop) that the same
/// call-site-expansion machinery a class-typed parameter already uses in fact does not have;
/// a bare `x = f()` where f returns `list[T]`/`array.array` typed x as UNKNOWN-turned-uint8,
/// so `x[i]` silently compiled into a bit-test of x's address instead of a list read; and even
/// once x was typed as the GC pointer it is, the call result's own Temporary was still
/// allocated 1 byte wide from that same UNKNOWN default, so its high byte was zeroed on the
/// way into x -- a divergence the optimized build's constant folding happened to paper over,
/// which is why <see cref="APointerReturnedThroughAnUnknownTypedResultSurvivesUnoptimized"/>
/// runs the unoptimized build specifically.
/// </summary>
[TestFixture]
public class ArrayArrayAsListTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session = new SimSession(PymcuCompiler.BuildFixture("array-array-as-list"));
    }

    private static string Output()
    {
        var uno = _session.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text;
    }

    [Test]
    public void AnArrayArrayHListBuiltAndReturnedByOneMethodSumsCorrectlyInAnother()
        => Output().Should().Contain("pulses_sum 18", "5 + 6 + 7, through a list[T] parameter with no static typecode");

    [Test]
    public void AnArrayArrayBListAtTheSameParameterUsesTheOtherCallersElementWidth()
        => Output().Should().Contain("buf_sum 15", "4 + 5 + 6, the SAME parameter, a different concrete element width");

    [Test]
    public void TheProgramRunsToItsEnd() => Output().Should().Contain("done");

    [Test]
    public void APointerReturnedThroughAnUnknownTypedResultSurvivesUnoptimized()
    {
        // Red before the fix: the optimized build (above) already answered "pulses_sum 18",
        // and PYMCU_NO_OPT=1 answered "pulses_sum 0" -- the SAME program, two different
        // numbers, because the un-widened call-result Temporary lost the GC pointer's high
        // byte on the way into `pulses`, and only the optimizer's own constant folding masked
        // it.
        var uno = new SimSession(PymcuCompiler.BuildFixtureUnoptimized("array-array-as-list")).Reset();
        uno.RunMilliseconds(400);
        uno.Serial.Text.Should().Contain("pulses_sum 18").And.Contain("buf_sum 15");
    }
}
