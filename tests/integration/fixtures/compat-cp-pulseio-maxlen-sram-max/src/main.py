# Two pulseio.PulseIn sharing the one capture ring the HAL has (PyMCU#406): the ring is
# sized to the LARGEST maxlen asked, not the last one constructed or the HAL's old fixed
# 128. D8 and D9 are both PB0/PB1, the same pin-change interrupt group, so both
# constructions are legal (pulse_isr can only sit at one vector).
#
# Read for its SRAM footprint (tests/integration/Tests/AVR/PulseioMaxlenSramTests.cs),
# not simulated: it builds, and the test parses dist/firmware.gas.asm for _bss_end.
import board
import pulseio
from pymcu.types import uint16

small = pulseio.PulseIn(board.D8, maxlen=4)
large = pulseio.PulseIn(board.D9, maxlen=16)
n: uint16 = len(small) + len(large)

while True:
    pass
