# CircuitPython rainbowio.colorwheel (pymcu-circuitpython#15).
#
# The module was absent, so every example that colours a strip by position had to carry its
# own wheel(). The wheel runs red, green, blue and back to red across 0 to 255; between the
# corners one channel falls by 3 a step while the next rises by 3.
#
# The three channels of each corner are read back at a BREAK:
#   GPIOR0 = red   GPIOR1 = green   GPIOR2 = blue
#
#   break  pos    expected
#   1      0      255,   0,   0
#   2      85       0, 255,   0
#   3      170      0,   0, 255
#   4      42     129, 126,   0   (halfway from red to green)
import board
from rainbowio import colorwheel
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.types import asm, uint8, uint32


def report(c: uint32):
    GPIOR0.value = uint8((c >> 16) & 0xFF)
    GPIOR1.value = uint8((c >> 8) & 0xFF)
    GPIOR2.value = uint8(c & 0xFF)


def main():
    report(colorwheel(0))
    asm("BREAK")
    report(colorwheel(85))
    asm("BREAK")
    report(colorwheel(170))
    asm("BREAK")
    report(colorwheel(42))
    asm("BREAK")

    while True:
        pass
