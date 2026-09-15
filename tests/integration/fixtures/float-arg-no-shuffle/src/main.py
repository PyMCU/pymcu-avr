# ATmega328P: a float operation's SECOND operand goes straight into the ABI
# register pair the soft-float routine reads it from.
#
# It used to be loaded into R22:R25 like the first one, moved four registers
# down, and the first operand stacked across the move: sixteen instructions
# where four will do, on every float operation in the program.
#
# The last line is the case that still cannot take the short path: an integer
# second operand has to be converted by __floatsisf, which both reads and
# returns R22:R25, so the first operand is still parked on the stack across it.
#
# Expected UART output (9600 baud):
#   2.75  (2.5 + 0.25)
#   2.25  (2.5 - 0.25)
#   0.63  (2.5 * 0.25, two decimals rounded)
#   10.0  (2.5 / 0.25)
#   5.0   (2.5 + half, a float variable rather than a literal)
#   7.5   (2.5 * k, k a uint8)
#   D     done marker
from pymcu.types import uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)

    a: float = 2.5
    b: float = 0.25
    half: float = 2.5
    k: uint8 = 3

    uart.print_float(a + b)
    uart.print_float(a - b)
    uart.print_float(a * b)
    uart.print_float(a / b)
    uart.print_float(a + half)
    uart.print_float(a * k)

    uart.write('D')
