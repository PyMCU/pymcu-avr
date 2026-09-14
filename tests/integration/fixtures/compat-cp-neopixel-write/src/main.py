# CircuitPython neopixel_write: the low-level one-wire write, below the neopixel
# library (pymcu-circuitpython#14).
#
# The three bytes are chosen so the test can read the waveform back as data. A WS2812
# takes green, red, blue, most significant bit first:
#
#   0x00  eight zeros, the shortest high time the protocol has, eight times over
#   0xFF  eight ones, the longest
#   0xA5  10100101, which alternates and so cannot be read right by a decoder that
#         has the two pulse widths swapped, or that latches on the wrong edge
#
# The pin is D6 (PD6). Interrupts are left alone: this program starts none, and what
# the test times is the emitter, not an interrupt policy.
import board
import digitalio
import neopixel_write
from pymcu.types import asm

frame = bytearray([0x00, 0xFF, 0xA5])


def main():
    pixel = digitalio.DigitalInOut(board.D6)
    pixel.direction = digitalio.Direction.OUTPUT
    asm("BREAK")
    neopixel_write.neopixel_write(pixel, frame)
    asm("BREAK")

    while True:
        pass
