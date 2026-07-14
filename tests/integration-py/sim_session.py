"""Per-test fast-reset session over an Arduino Uno simulation.

Python port of ``tests/integration/SimSession.cs``, backed by the ``avr8sharp`` package. The
power-on snapshot is captured *before* the HEX is loaded (so it preserves peripheral power-on
defaults), and :meth:`reset` restores it natively — restoring RAM/registers, resetting timers
and the CPU, zeroing the cycle counter, and clearing the serial probe — without re-parsing the
HEX or re-allocating the simulation.
"""

from __future__ import annotations

from avr8sharp import ArduinoUno


class SimSession:
    """Holds one :class:`~avr8sharp.ArduinoUno` plus its power-on snapshot for cheap resets."""

    def __init__(self, hex_content: str) -> None:
        self._sim = ArduinoUno()
        # Snapshot BEFORE loading the HEX: captures peripheral power-on defaults
        # (e.g. UCSRA bits set by the USART constructor) and SP/SREG reset state.
        self._sim.snapshot()
        self._sim.with_hex(hex_content)

    def reset(self) -> ArduinoUno:
        """Restores the simulation to its power-on state and returns it, ready for a fresh run."""
        self._sim.restore()
        return self._sim

    @property
    def sim(self) -> ArduinoUno:
        return self._sim

    def close(self) -> None:
        self._sim.close()
