# ATmega328P: `for v in <list>` keeps its loop bound intact across iterations.
#
# The list length lives in a linear-scan temp register for the whole loop, but
# the body's element-address arithmetic used the same register as scratch.
# After one iteration the bound became i+2, so `i+1 < i+2` held forever: the
# loop ran off the end of the list, summed 251 bytes of arbitrary RAM, and only
# stopped when the 8-bit index wrapped. The sum came out different on every
# pass. Two passes over two lists must print the same exact sums every time.
#
# Expected UART output (9600 baud), repeated twice:
#   100    sum of [10, 20, 30, 40]
#   612    sum of [255, 1, 100, 0, 256]
#   D      done marker
from pymcu.types import uint8, uint16
from pymcu.hal.uart import UART
from pymcu.time import delay_ms


def main():
    uart = UART(9600)

    n = 0
    while n < 2:
        a: list[uint8] = [10, 20, 30, 40]
        total = 0
        for v in a:
            total += v
        uart.print_int16(total)

        b: list[uint16] = [255, 1, 100, 0, 256]
        big = 0
        for w in b:
            big += w
        uart.print_int16(big)

        n += 1

    uart.write('D')

    while True:
        delay_ms(1000)
