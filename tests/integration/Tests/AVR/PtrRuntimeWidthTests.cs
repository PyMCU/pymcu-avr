// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for the ACCESS WIDTH of Store/LoadIndirect through a runtime
/// ptr[T]: the pointer's element type decides how many bytes move, not the stored
/// value's magnitude. Small constants written through ptr[uint16]/ptr[uint32] must
/// write every element byte (the optimizer copy-forwards the constant into the
/// store), and a 16-bit += must read and write both bytes. Surrounding SRAM is
/// poisoned with 0xEE so a narrowed access leaves visible garbage.
/// </summary>
[TestFixture]
public class PtrRuntimeWidthTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("ptr-runtime-width"));

    [Test]
    public void RuntimePointer_ElementWidth_GovernsStoreLoadWidth()
    {
        var uno = _session.Reset();
        uno.RunUntilSerialBytes(uno.Serial, 11, maxMs: 500);  // "PW\n" (3) + 8 data bytes
        var bytes = uno.Serial.Bytes;
        var data = bytes.Skip(3).ToArray();

        data[0].Should().Be(0x12, "the low byte of a 16-bit constant store");
        data[1].Should().Be(0x00, "the HIGH byte of a 16-bit store of 0x12 - 0xEE means only one byte was written");
        data[2].Should().Be(0x13, "low byte of 0x0112 + 1");
        data[3].Should().Be(0x01, "high byte of 0x0112 + 1 - 0x00 means the RMW loaded/stored only one byte");
        data[4].Should().Be(0x34, "the low byte of a 32-bit constant store");
        data[5].Should().Be(0x00, "byte 1 of a 32-bit store of 0x34");
        data[6].Should().Be(0x00, "byte 2 of a 32-bit store of 0x34");
        data[7].Should().Be(0x00, "byte 3 of a 32-bit store of 0x34");
    }
}
