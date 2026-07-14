"""Integration test: examples/blink compiled by PyMCU, run on the avr8sharp emulator.

The blink firmware drives PB5 (Arduino pin 13) high, waits 1000 ms, low, waits 1000 ms, repeat.
Mirrors what the C#/NUnit suite asserts — proving PyMCU integration tests can be written in
Python against the very same engine.
"""

from __future__ import annotations

import avr8sharp as a


def test_blink_drives_pb5_high_initially(blink_session: a.ArduinoUno):
    # After the first led.high(), PB5 is High while the 1000 ms delay runs.
    blink_session.run_ms(50)
    assert blink_session.port_b.pin_high(5)


def test_blink_toggles_pb5(blink_session: a.ArduinoUno):
    # High for ~1000 ms, then Low for ~1000 ms.
    blink_session.run_ms(50)
    assert blink_session.port_b.pin_high(5), "PB5 should be High shortly after start"

    blink_session.run_ms(1000)  # cross into the low phase
    assert blink_session.port_b.pin_low(5), "PB5 should be Low ~1 s in"

    blink_session.run_ms(1000)  # cross back into the high phase
    assert blink_session.port_b.pin_high(5), "PB5 should be High again ~2 s in"


def test_reset_round_trips(blink_hex: str):
    from sim_session import SimSession

    session = SimSession(blink_hex)
    sim = session.reset()
    sim.run_ms(50)
    assert sim.port_b.pin_high(5)
    cycles_before = sim.cpu.cycles
    assert cycles_before > 0

    # A fresh reset must rewind everything to power-on.
    sim2 = session.reset()
    assert sim2.cpu.cycles == 0
    assert not sim2.port_b.pin_high(5)
    session.close()
