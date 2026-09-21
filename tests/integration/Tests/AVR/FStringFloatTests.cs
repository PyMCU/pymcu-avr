using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/fstring-float (f"{v:.Nf}" / f"{v:W.Nf}").
///
/// PyMCU floats are IEEE-754 singles, so the oracle is CPython formatting the
/// float64 that holds the exact float32 value -- e.g. 2.675 is
/// 2.6749999523162841796875 and formats "2.67", not "2.68". The digits come from
/// the exact decimal expansion (uart_text._float_fmt_digits), so the .5
/// boundaries below are real ties and land half-to-even like CPython's.
///
/// The whole serial block is compared, not a substring: padding inside a field
/// is only visible in a whole-line comparison.
/// </summary>
[TestFixture]
public class FStringFloatTests
{
    private static SimSession _session = null!;

    // CPython's output for the float32 value each literal denotes:
    //   struct.unpack('f', struct.pack('f', v))[0].__format__(spec)
    private const string Expected =
        "FFMT\n" +
        "a1=23.5\n" +
        "a2=23.46\n" +
        "a6=23.455999\n" +
        "aW=   23.4560\n" +
        "h0=0\n" +          // 0.5 -> "0": exact tie, half to even
        "i0=2\n" +
        "j0=2\n" +          // 2.5 -> "2": exact tie, half to even (not 3)
        "k0=4\n" +
        "e2=0.12\n" +       // 0.125 -> "0.12": exact tie, half to even
        "g2=0.38\n" +
        "f2=2.67\n" +       // float32 2.675 is 2.67499995231...
        "m2=1.00\n" +       // float32 1.005 is 1.00499999523...
        "pd=3.141593\n" +   // {:f} defaults to precision 6
        "p3=3.142\n" +
        "b2=12345.68\n" +
        "t4=0.0010\n" +
        "n1=-12.3\n" +
        "qW=  -1.500\n" +
        "qZ=-0001.50\n" +   // zero-pad keeps the sign in front
        "z0=-0\n" +         // negative zero, like CPython's "-0"
        "aS=  23.456\n" +
        "aZ=00023.46\n" +
        "c1=1000.0\n" +     // rounding carries into the integer part
        "r1=255.0\n" +      // int operand under an f spec
        "buf=23.46|-12.3\n" +
        "done\n";

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("fstring-float"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 500);
        return uno;
    }

    [Test]
    public void FormattedFloats_MatchCPythonExactly()
    {
        Boot().Serial.Text.Should().Be(Expected);
    }
}
