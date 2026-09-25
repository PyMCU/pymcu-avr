using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/ptr-field-element-width (PyMCU#484, PyMCU#485).
///
/// A field holding a register pointer lost its element width unless the value arrived
/// through a `-> ptr[T]`-annotated selector, so `.value` on it wrote TWO bytes into I/O
/// space and clobbered the neighbouring register. GPIOR2 is the witness here: seeded with
/// 0xA5 before each write aimed at GPIOR1, it has to still read 0xA5 afterwards.
///
/// The fourth line pins the annotated spelling, which used to be refused with "Array size
/// 'uint8' is not a compile-time constant"; the fifth pins that an annotation asking for a
/// 16-bit pair still gets one.
/// </summary>
[TestFixture]
public class PtrFieldElementWidthTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("ptr-field-element-width"));

    [Test]
    public void APointerFieldWritesItsOwnRegisterAndNotTheNextOne()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Be("17 165\n18 165\n19 165\n20 165\n4660\ndone\n",
            "each write lands on GPIOR1 and leaves GPIOR2 at 0xA5 = 165; a field that " +
            "lost its element width stored two bytes and cleared the witness");
    }
}
