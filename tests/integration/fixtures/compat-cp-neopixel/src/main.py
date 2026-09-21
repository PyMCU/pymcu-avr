# CircuitPython neopixel + adafruit_pixelbuf, unmodified upstream sources: the
# user-facing driver the compat layer is for. `pixels[i] = (r, g, b)` exercises the
# whole chain -- the `isinstance` guards in __setitem__ and _parse_color, the
# compile-time byteorder string parsing in PixelBuf.__init__, the tuple return of
# parse_byteorder, and the arena-backed frame buffer out through neopixel_write.
#
# The strip is 4 pixels on D6 (PD6) in GRB order. Two frames go out on the wire:
# fill(0) blanks every pixel (auto_write shows it), then pixels[0] = (1, 2, 3)
# shows again. On the wire GRB means green first, so pixel 0 sends 2, 1, 3 and the
# other three pixels send zeros. The test decodes the pulses on PD6 and reads the
# bytes back.
import board
import neopixel
from pymcu.types import asm

pixels = neopixel.NeoPixel(board.D6, 4)


def main():
    pixels.fill(0)
    asm("BREAK")
    pixels[0] = (1, 2, 3)
    asm("BREAK")

    while True:
        pass
