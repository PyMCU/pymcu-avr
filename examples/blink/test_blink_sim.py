"""Turnkey simulation test for the blink example (pymcu-test plugin).

The `mcu` fixture auto-discovers this project, compiles it, picks the Arduino Uno board from
[tool.pymcu] target = "atmega328p", and loads the firmware — no setup needed.
"""


def test_pb5_high_then_toggles(mcu):
    mcu.run_ms(50)
    assert mcu.port_b.pin_high(5), "PB5 high during the first 1 s delay"

    mcu.run_ms(1000)
    assert mcu.port_b.pin_low(5), "PB5 low ~1 s in"
