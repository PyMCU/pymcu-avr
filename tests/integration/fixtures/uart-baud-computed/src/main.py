# PyMCU -- uart-baud-computed: UBRR comes from the clock and the rate, not a table.
#
# Regression for PyMCU#136. uart_init was an if/elif over five literal baud rates with values
# hard-coded for 16 MHz and NO else, so any other rate left UBRR at 0, which is 1 Mbaud on a
# 16 MHz part. Clean build, no diagnostic, a UART running a hundred times too fast.
#
# 4800 and 2400 are standard rates (plenty of GPS and RS-485 devices sit at 4800), and 250000
# is the rate people pick on AVR precisely because it divides exactly and has zero error.
#
# The divisor is now computed from __FREQ__ and the requested rate. Both are compile-time
# constants, so the expression folds and what is emitted is still two register writes.
#
# Each init copies UBRR0L into a different GPIOR so the test can read all three at the BREAK.
# Expected: GPIOR0 = 103 (9600), GPIOR1 = 207 (4800), GPIOR2 = 3 (250000).
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2, UBRR0L, UBRR0H, UCSR0A
from pymcu.hal.uart import UART
from pymcu.types import asm


def main() -> None:
    a = UART(9600)
    GPIOR0.value = UBRR0L.value

    b = UART(4800)
    GPIOR1.value = UBRR0L.value

    c = UART(250000)
    GPIOR2.value = UBRR0L.value

    asm("BREAK")
    while True:
        pass


main()
