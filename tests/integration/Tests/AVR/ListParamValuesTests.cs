using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A driver takes a table of numbers and a buffer, and keeps both in fields (PyMCU#314,
/// PyMCU#315).
///
/// Both used to build clean and run wrong: the field became a scalar, so every subscript read
/// the zero nothing had written, and the buffer's address went into that scalar so
/// self._data[i] was refused as a bit index. Nothing here can be checked by compiling alone,
/// which is why it is a fixture.
///
/// The second table is the module-level list whose name is also the parameter's. That is what
/// caught the remaining defect: the literal handed to the FIRST driver was answered by the
/// module list, and both drivers reported 7, 8, 9.
///
/// The seed (GPIOR0 = 5) fills the buffer on the chip.
/// </summary>
[TestFixture]
public class ListParamValuesTests
{
    private const int Gpior0Addr = 0x3E;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("list-param-values"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = 5;
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void AConstantSubscriptOfTheFieldIsTheValue()
        => Transcript().Should().Contain("A 10\nB 30\n");

    [Test]
    public void AForOverTheFieldWalksEveryValue()
        => Transcript().Should().Contain("C 60\n", "10 + 20 + 30");

    [Test]
    public void LenOfTheFieldIsTheCount()
        => Transcript().Should().Contain("D 3\n");

    [Test]
    public void ATableReachedByNameIsItsOwnTable()
        => Transcript().Should().Contain("E 7\nF 24\n", "the module list 7, 8, 9, not the literal 10, 20, 30");

    [Test]
    public void ABufferKeptInAFieldIsWrittenAndReadByRunTimeIndex()
        => Transcript().Should().Contain("G 5\nG 6\nG 7\nG 8\n");
}
