"""Turnkey simulation test for an ATtiny84 project (pymcu-test plugin).

Exercises the full chain: PyMCU's per-chip ATtiny codegen (RJMP-only, RAMEND 0x25F) -> avr8sharp
ATtinyX4 preset, auto-selected from [tool.pymcu] target = "attiny84".
"""


def test_blinks_pa0(mcu):
    mcu.run_ms(50)
    # The firmware's startup set SP to the ATtiny84 RAMEND (0x25F) — proving the codegen emitted
    # the per-chip RAMEND, not the ATmega 0x8FF fallback that avr-as could not even assemble.
    assert mcu.cpu.sp == 0x025F
    assert mcu.port_a.pin_high(0), "PA0 high during the first 100 ms delay"

    mcu.run_ms(120)
    assert mcu.port_a.pin_low(0), "PA0 low after ~120 ms"
