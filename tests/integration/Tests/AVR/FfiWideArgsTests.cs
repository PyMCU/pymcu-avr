using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Regression tests for tests/integration/fixtures/ffi-wide-args.
///
/// An <c>@extern</c> function has no body, so it never reaches the IR function list and
/// the backend had no record of its declared parameter widths. Every argument was sized
/// by the width of the VALUE instead: <c>wide_sum3(1, 2, 3)</c> loaded R24/R22/R20 and
/// left R25/R23/R21 holding whatever the previous call had put there.
///
/// The fixture calls <c>wide_echo0(0x1234, 0x5678, 0x9ABC)</c> first precisely so those
/// high halves are non-zero when the small-literal call runs.
///
/// Expected UART output:
///   "WIDE\n"     -- boot banner
///   "B:4660\n"   -- wide_echo0(0x1234, ...) = 0x1234 = 4660
///   "S:6\n"      -- wide_sum3(1, 2, 3) = 6
///   "V:600\n"    -- wide_sum3(n, n, n), n a uint8 holding 200
///   "E:65538\n"  -- wide_echo32(0x00010002); swapped halves would read 131073
///   "T:65539\n"  -- wide_sum32(0x00010002, 1)
///   "F:6\n"      -- wide_scale_to_u16(1.5, 4.0) = 6
///   "G:7\n"      -- wide_scale_to_u16(3, 2.5) = 7
///   "OK\n"       -- done
/// </summary>
[TestFixture]
public class FfiWideArgsTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("ffi-wide-args"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "WIDE\n", maxMs: 200);
        return uno;
    }

    [Test]
    public void Echo_LargeValue_RoundTrips()
    {
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("B:4660\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("B:4660",
            "wide_echo0(0x1234, 0x5678, 0x9ABC) must return 0x1234 = 4660");
    }

    [Test]
    public void SmallLiterals_FillBothHalvesOfEachArgumentPair()
    {
        // wide_sum3(1, 2, 3) = 6. With the high halves left over from the previous
        // call the sum came out as 0x1201 + 0x5602 + 0x9A03, not 6.
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("S:6\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("S:6\n",
            "wide_sum3(1, 2, 3) must return 6; a stale high byte in any argument pair changes the sum");
    }

    [Test]
    public void Uint8Variable_IsWidenedToTheDeclaredParameter()
    {
        // n is a uint8 holding 200; the parameter is uint16, so each argument
        // pair needs its high half zeroed rather than left untouched.
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("V:600\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("V:600",
            "wide_sum3(n, n, n) with n = 200 must return 600");
    }

    [Test]
    public void Uint32_FirstArgument_UsesTheCLayout()
    {
        // avr-gcc reads a 32-bit arg0 as byte0 in R22 .. byte3 in R25. PyMCU's own convention
        // anchors it at R24 with bytes 2-3 in R22:R23, so 0x00010002 arrived as 0x00020001
        // and echoed back 131073 instead of 65538.
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("E:65538\n"), maxMs: 400);
        uno.Serial.Text.Should().Contain("E:65538",
            "wide_echo32(0x00010002) must return 65538");
        uno.Serial.Text.Should().NotContain("E:131073",
            "131073 = 0x00020001 is the same value with its 16-bit halves swapped");
    }

    [Test]
    public void Uint32_SecondArgument_StillContiguous()
    {
        // The second 32-bit slot is contiguous from its base under both layouts; this guards
        // against the C-ABI anchoring being applied to the wrong slot.
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("T:65539\n"), maxMs: 400);
        uno.Serial.Text.Should().Contain("T:65539",
            "wide_sum32(0x00010002, 1) must return 65539");
    }

    [Test]
    public void FloatLiteral_ReachesAFloatParameterAsAFloat()
    {
        // The float literal used to be rounded to an int before the call, so C read an
        // integer bit pattern as a float: 1.5 * 4.0 came out as anything but 6.
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("F:6\n"), maxMs: 600);
        uno.Serial.Text.Should().Contain("F:6\n",
            "wide_scale_to_u16(1.5, 4.0) must return 6");
    }

    [Test]
    public void IntegerLiteral_IsPromotedInAFloatParameter()
    {
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("G:7\n"), maxMs: 600);
        uno.Serial.Text.Should().Contain("G:7\n",
            "wide_scale_to_u16(3, 2.5) must return 7");
    }

    [Test]
    public void AllProbes_Done_Marker_Present()
    {
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("OK\n"), maxMs: 500);
        uno.Serial.Text.Should().Contain("OK");
    }
}
