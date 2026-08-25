using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/overload-facade (the facade half of PyMCU#75).
///
/// An overloaded constructor reached through a re-exporting facade failed twice over.
/// First the build stopped, because the re-export carried the suffixed overload keys to
/// the facade prefix but not the record that the name was overloaded, so the constructor
/// was found under neither the bare key nor the overload set. Then, with that fixed, the
/// build succeeded and chose the wrong overload, because an argument that is a field was
/// typed by inference alone and a const[str] field bound to the numeric overload.
///
/// The tag distinguishes which overload ran: 20 + k for the string one, 10 + k for the
/// numeric one. k comes from GPIOR0 so the value cannot fold to a constant, which is what
/// keeps this a test of the selection rather than of the folder.
/// </summary>
[TestFixture]
public class OverloadFacadeTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("overload-facade"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void TheConstructorResolvesThroughTheFacade()
    {
        // Before the fix this fixture did not build at all: "'Low' is not exported by mid".
        Boot().Should().Contain("done\n", "the program reached the end");
    }

    [Test]
    public void TheStringOverloadIsChosenForAFieldArgument()
    {
        // 10 here means the numeric overload ran for a const[str] field.
        Boot().Should().StartWith("20\n", "self._name is a string, so Low(str, k) is the match");
    }
}
