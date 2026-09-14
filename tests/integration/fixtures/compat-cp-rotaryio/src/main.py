# CircuitPython rotaryio.IncrementalEncoder: where a two-track knob has turned to
# (pymcu-circuitpython#12).
#
# The module was absent, and the first attempt at it never moved: a global shared between an
# interrupt handler and the main program was allocated in the callee-saved pool R2-R15, and
# every handler's epilogue restored it, so each edge's write was undone before the handler
# returned (PyMCU#328). That is what this fixture measures. Eight line changes are two
# detents of a common knob, and the number the program reports has to be 2.
#
# The test drives D2 and D3 through the quadrature sequence during each wait and reads back:
#   break  GPIOR0              GPIOR1              GPIOR2
#   2      position low byte   position high byte  -
#   3      position low byte   position high byte  -
import board
import rotaryio
from pymcu.chips.atmega328p import GPIOR0, GPIOR1
from pymcu.time import delay_ms
from pymcu.types import asm, uint8, int32


def report(p: int32):
    GPIOR0.value = uint8(p & 0xFF)
    GPIOR1.value = uint8((p >> 8) & 0xFF)


def main():
    knob = rotaryio.IncrementalEncoder(board.D2, board.D3)

    asm("BREAK")               # the test turns the knob during the wait below
    delay_ms(20)
    report(knob.position)
    asm("BREAK")               # and turns it again during this one

    delay_ms(20)
    report(knob.position)
    asm("BREAK")

    while True:
        pass
