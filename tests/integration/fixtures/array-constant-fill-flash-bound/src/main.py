# A constant-sized bytearray lowers to one ArrayStore per element, and each one is a
# whole instruction: 2 bytes inside the STD Y+q window, 4 bytes (STS) past it. On the
# unmodified adafruit_ssd1306 simpletest the single line `self.buffer = bytearray(513)`
# was 1972 of the 4188 bytes of the image. A counted loop writes the same bytes in 12
# to 16, so the backend replaces the run whenever the loop is strictly smaller.
#
# Read for its flash size (tests/integration/Tests/AVR/ArrayConstantFillFlashBoundTests.cs),
# not simulated: it only needs to build. 513 is the ssd1306 buffer length, the size that
# motivated the change, and it exercises the 16-bit counter path (past 256).
from pymcu.types import uint8

buffer = bytearray(513)
buffer[0] = 0x40
x: uint8 = buffer[512]

while True:
    pass
