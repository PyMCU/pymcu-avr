# pulseio.PulseIn(pin, maxlen=1): the capture ring sized to what one instance asks for
# (PyMCU#406), not the HAL's 128-pulse maximum. A PulseIn(maxlen=1) used to pay for 256
# bytes of SRAM -- 12.5 percent of an ATmega328P -- for a driver that reads one pulse.
#
# This program is read for its SRAM footprint (tests/integration/Tests/AVR/
# PulseioMaxlenSramTests.cs), not simulated: it builds, and the test parses
# dist/firmware.gas.asm for _bss_end.
import board
import pulseio
from pymcu.types import uint16

pulses = pulseio.PulseIn(board.D2, maxlen=1)
n: uint16 = len(pulses)

while True:
    pass
