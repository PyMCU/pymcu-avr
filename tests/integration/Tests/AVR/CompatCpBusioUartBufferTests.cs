using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-busio-uart-buffer (pymcu-circuitpython#22): in_waiting is a count of
/// the bytes waiting, and readinto returns what it got when the timeout runs out. Before,
/// in_waiting was the receive-complete flag and answered 0 or 1 however many bytes had
/// arrived, and readinto blocked on every byte for ever whatever timeout it was given.
/// </summary>
[TestFixture]
public class CompatCpBusioUartBufferTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-busio-uart-buffer"));

    /// <summary>Feeds `bytes` on the receive line, then reports in_waiting and the read.</summary>
    private static (byte InWaiting, byte Read, byte First, byte Second) Run(params byte[] feed)
    {
        var uno = _session.Reset();
        uno.RunToBreak(40_000_000);
        // The firmware waits 10 ms after this BREAK, which is the window these cycles spend:
        // each byte needs a few for the receive interrupt to move it into the ring.
        foreach (var b in feed)
        {
            uno.Serial.InjectByte(b);
            uno.RunCycles(2000);
        }
        uno.RunInstructions(1);
        uno.RunToBreak(40_000_000);
        var inWaiting = uno.Data[Gpior0Addr];
        uno.RunInstructions(1);
        uno.RunToBreak(40_000_000);
        return (inWaiting, uno.Data[Gpior0Addr], uno.Data[Gpior1Addr], uno.Data[Gpior2Addr]);
    }

    [Test]
    public void InWaitingCountsEveryByteWaiting()
    {
        Run(0x41, 0x42, 0x43).InWaiting.Should().Be(3, "three bytes arrived, not 'at least one'");
    }

    [Test]
    public void InWaitingIsZeroWithNothingWaiting()
    {
        Run().InWaiting.Should().Be(0);
    }

    [Test]
    public void AFullBufferReadsInOrder()
    {
        var r = Run(0x41, 0x42, 0x43, 0x44);
        r.Read.Should().Be(4);
        r.First.Should().Be(0x41);
        r.Second.Should().Be(0x42);
    }

    [Test]
    public void AShortReadReturnsWhatItGotInsteadOfBlocking()
    {
        var r = Run(0x41, 0x42);
        r.Read.Should().Be(2, "the buffer holds four and two arrived; the read gives up and says two");
        r.First.Should().Be(0x41);
        r.Second.Should().Be(0x42);
    }

    [Test]
    public void AReadWithNothingToReadReturnsZero()
    {
        Run().Read.Should().Be(0, "a read that never returns is not a timeout");
    }
}
