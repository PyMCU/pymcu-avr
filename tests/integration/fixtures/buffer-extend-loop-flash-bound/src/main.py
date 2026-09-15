# PyMCU#411: growing a buffer (PyMCU#362) by a large compile-time constant used to unroll
# into one store per new slot no matter how many. 70 new slots, 69 past the measured AVR
# crossover of 13, is the number PulseCapture (PyMCU#406) grows its ring by for a 70-pulse
# NEC frame; unrolled that cost 432 bytes of flash on a 754-byte program.
#
# Read for its flash size (tests/integration/Tests/AVR/BufferExtendLoopFlashBoundTests.cs),
# not simulated: it only needs to build.
from pymcu.types import uint8

_BUFFER = bytearray(1)
_BUFFER.extend(bytes(70))
x: uint8 = _BUFFER[69]

while True:
    pass
