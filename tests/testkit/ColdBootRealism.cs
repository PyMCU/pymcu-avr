using Avr8Sharp.TestKit;

namespace PyMCU.TestKit;

/// <summary>
/// A real ATmega328P's datasheet guarantees the reset value of its I/O
/// registers and nothing else: R0-R31 (data addresses 0x00-0x1F) and SRAM
/// (0x100 upward) hold whatever cold power-on left there. avr8sharp's
/// simulation state is a freshly allocated C# byte[] regardless, so it
/// always starts at zero -- a firmware that silently relies on a register or
/// a byte of SRAM already being zero passes every test that only runs it in
/// the emulator and can hang or misbehave the first time it meets real
/// silicon whose cold-boot pattern is not zero (PyMCU-gemlife-lockinit,
/// 2026-09-29: busio.I2C's try_lock/unlock share a pool register nothing
/// wrote before its first read; only fixed by making the AVR backend zero
/// every R2-R15 pool home at boot, since the register that ends up holding
/// any one escaping field depends on the whole program's register pressure
/// and cannot be predicted from source).
///
/// <see cref="Poison"/> is the tool for catching the NEXT one of these: fill
/// the state that real cold silicon leaves undefined with a non-zero pattern
/// before the first instruction runs, so a test that still passes actually
/// demonstrates the firmware does not depend on an assumption avr8sharp
/// happens to make true for free.
///
/// Deliberately opt-in here, not the emulator's default: turning it on
/// wholesale surfaces every existing fixture that relies on implicit zero
/// somewhere, and each one needs its own look before anyone can tell a real
/// bug from a false alarm (an oracle whose CPython reference also runs from
/// a zeroed Python object, for instance, would need its own accounting, not
/// silently start failing). Call it explicitly per test or test family
/// while that triage happens.
/// </summary>
public static class ColdBootRealism
{
    /// <summary>
    /// Fills R0-R31 (except R1, the compiler's own zero-register convention,
    /// which every program's own CRT sets with <c>eor r1, r1</c> before
    /// anything else runs) and SRAM 0x100..0x8FF with <paramref name="value"/>.
    /// Call after <c>WithHex</c> and before running any instruction.
    /// </summary>
    public static void Poison(AvrTestSimulation uno, byte value = 0xFF)
    {
        for (var r = 0; r <= 0x1F; r++)
            if (r != 1) uno.Data[r] = value;
        for (var addr = 0x100; addr <= 0x8FF; addr++)
            uno.Data[addr] = value;
    }
}
