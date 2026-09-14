# PyMCU -- range-loop-var-after: the loop variable keeps Python's value after the loop (PyMCU#285)
#
# After a constant range short enough to unroll, the loop variable read 0: the unroller bound
# the name to a constant per iteration and dropped the binding without ever storing the last
# value. After a runtime range loop it held `stop`, the first value not visited, where Python
# leaves the last one visited. Loops that leave through `break` were already right.
#
# `lim` comes from GPIOR0 (the test seeds 4) so the break in C cannot fold away.
#
# Expected UART (115200), seed 4:
#   A 3 2
#   B 19
#   C 1
#   D 4
#   E 4
#   F 4
#   END
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


def main():
    lim: uint8 = GPIOR0.value

    x: uint8 = 0
    for i in range(3):
        x = x + i
    print("A", x, i)

    for k in range(20):
        x = x + 1
    print("B", k)

    for d in range(20, 0, -1):
        x = x + 1
    print("C", d)

    for m in range(10):
        if m == 4:
            break
    print("D", m)

    for n in range(6):
        if n == lim:
            break
    print("E", n)

    for q in range(5):
        if q == 1:
            continue
    print("F", q)

    print("END")

    while True:
        pass


main()
