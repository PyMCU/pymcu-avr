using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-rainbowio (pymcu-circuitpython#15): colorwheel(pos) gives the colour
/// at a position on the wheel. The module was absent, so every example that colours a strip
/// by position carried its own wheel().
/// </summary>
[TestFixture]
public class CompatCpRainbowioTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-rainbowio"));

    private static (byte R, byte G, byte B)[] Run()
    {
        var uno = _session.Reset();
        var reads = new (byte, byte, byte)[4];
        for (var i = 0; i < reads.Length; i++)
        {
            if (i > 0) uno.RunInstructions(1);
            uno.RunToBreak(2_000_000);
            reads[i] = (uno.Data[Gpior0Addr], uno.Data[Gpior1Addr], uno.Data[Gpior2Addr]);
        }
        return reads;
    }

    [TestCase(0, 255, 0, 0, "red")]
    [TestCase(1, 0, 255, 0, "green")]
    [TestCase(2, 0, 0, 255, "blue")]
    public void TheCornersAreTheThreePrimaries(int step, int r, int g, int b, string colour)
    {
        var c = Run()[step];
        (c.R, c.G, c.B).Should().Be(((byte)r, (byte)g, (byte)b), colour);
    }

    [Test]
    public void BetweenTwoCornersOneChannelFallsWhileTheNextRises()
    {
        // pos 42 is halfway from red to green: 255 - 42*3 = 129, and 42*3 = 126.
        var c = Run()[3];
        c.R.Should().Be(129);
        c.G.Should().Be(126);
        c.B.Should().Be(0);
    }
}
