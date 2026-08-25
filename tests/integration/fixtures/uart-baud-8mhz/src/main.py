# PyMCU -- uart-baud-8mhz: `frequency` in pyproject.toml reaches the UART.
#
# The other half of PyMCU#136. The UBRR table was written for 16 MHz with no reference to the
# configured clock, so a part running at 8 MHz asked for 9600 and got 4808: every byte wrong,
# with a clean build and nothing to read that would say so.
#
# At 8 MHz, UBRR = round(8000000 / (16 * 9600)) - 1 = 51. At 16 MHz the same call gives 103,
# which is what the sibling fixture uart-baud-computed pins.
#
# Expected: GPIOR0 = 51.
from pymcu.chips.atmega328p import GPIOR0, UBRR0L
from pymcu.hal.uart import UART
from pymcu.types import asm


def main() -> None:
    a = UART(9600)
    GPIOR0.value = UBRR0L.value

    asm("BREAK")
    while True:
        pass


main()
