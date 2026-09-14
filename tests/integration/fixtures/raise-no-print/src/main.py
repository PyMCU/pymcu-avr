# An unhandled raise in a program with no print() still names itself (PyMCU#340).
#
# The unhandled path writes E:<TypeName> to UART0, and UART0 was only set up when the driver
# saw `print(` or an explicit `UART(` in the sources. This program has neither, so the message
# went into a transmitter that was off and the board just stopped: the silent halt the
# limitations page promises never happens, wearing another hat.
#
# Nothing here writes to the UART. Everything the test reads on the wire was put there by the
# unhandled-exception path itself, including turning the transmitter on.
#
# The test seeds GPIOR0 before running. With 90 the raise fires; with anything else the program
# writes 0x55 to GPIOR1 and then blinks D13, which is how the run is told apart from a crash --
# and the blink is what makes this fixture observable to the differential harness, which
# compares pins and the wire and skips a program that moves neither.
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, DDRB, PORTB
from pymcu.types import uint8


def main():
    code: uint8 = GPIOR0.value
    if code == 90:
        raise ValueError
    GPIOR1.value = 0x55
    DDRB[5] = 1
    while True:
        PORTB[5] = 1
        PORTB[5] = 0
