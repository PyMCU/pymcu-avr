# PyMCU -- string-runtime-index: subscripting a string with a run-time index.
#
# PyMCU#86 reported this as rejected with "Bit index must be constant for reading", a message
# about an operation the program does not contain. It builds now, so the fixture is what says
# the answer is also right rather than merely produced.
#
# GPIOR0 reads 0 out of reset, so the index is 0 and the character is 'a'.
#
# Expected UART output:
#   a
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


def main():
    seed: uint8 = GPIOR0.value
    s = "abcd"
    print(s[seed & 3])
    print("done")
