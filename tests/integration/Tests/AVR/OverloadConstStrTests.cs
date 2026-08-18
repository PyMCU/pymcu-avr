// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Overload selection reads the argument types. Registered keys spell parameter
/// types raw ("const[str]") while the call site spells them normalized ("str"),
/// so for a const[...] parameter the exact lookup never hits and the call used to
/// fall through to "first overload of the right arity" — declaration order. Each
/// __init__ tags itself; the integer overloads are declared first on purpose,
/// since that is the order that used to swallow a string argument.
/// </summary>
[TestFixture]
public class OverloadConstStrTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("overload-const-str"));

    [Test]
    public void IntegerArgumentsPickTheIntegerOverloads()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "s2=3 s3=4\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("i2=1 i3=2");
    }

    [Test]
    public void StringArgumentsPickTheStringOverloads()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "s2=3 s3=4\n", maxMs: 3000);
        // Declaration order would have answered 1 and 2 here.
        uno.Serial.Should().ContainLine("s2=3 s3=4");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
