// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/romfs-protocol: the whole RFC 0008 file protocol over one embedded
/// 13-byte <c>data.txt</c> ("PyMCU\nsecond\n"), auto-embedded from the literal
/// in <c>open("data.txt", "rb")</c> -- no <c>files = [...]</c> entry.
///
/// Exercised: <c>with open()</c> (enter/exit), <c>read(n)</c> folded while the
/// position is compile-time, <c>readline(max)</c> (the newline kept, pos left
/// just past it), <c>readinto(buf)</c>, <c>seek</c> in all three whences,
/// <c>tell</c>, <c>struct.unpack(fmt, f.read(n))</c> fused to a flash read,
/// <c>os.stat(name)[6]</c> = 13, <c>os.listdir()</c> unrolled to the sorted
/// embedded names, read past the end empty (CPython semantics, not an error),
/// and one run-time-position read so the flash-load path is covered too.
/// </summary>
[TestFixture]
public class RomfsProtocolTests
{
    private const string Expected =
        "80\n85\n5\n" +          // hdr[0]='P', hdr[4]='U', tell=5
        "10\n1\n6\n" +           // readline: "\n", len 1, pos just past it
        "0\n" +                  // seek(0,2) then read(1): empty at EOF
        "5\n85\n0\n" +           // readinto=5 bytes, buf[4]='U', tell=0 after seek(0)
        "80\n121\n77\n" +        // pair='P','y'; struct.unpack("<B", read(1))='M'
        "111\n" +                // seek(-4,2) then read(1)[0]='o'
        "13\n" +                 // os.stat("data.txt")[6]
        "data.txt\n" +           // os.listdir()
        "True\n" +               // run-time-position read: len(read(2))>=0
        "END\n";

    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("romfs-protocol"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("romfs-protocol"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void Protocol_MatchesCPythonSemantics()
    {
        FullRun(_session).Serial.Text.Should().Be(Expected,
            because: "every romfs operation must produce exactly what CPython's file "
                + "object produces on the same bytes");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Be(Expected,
            because: "both front ends must lower the romfs protocol identically");
    }
}
