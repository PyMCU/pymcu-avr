# float-compare: comparing floats goes through the soft-float runtime.
#
# The integer ordering of a float's bits is not the ordering of the values it
# stands for -- IEEE754 negatives compare ABOVE positives as unsigned -- so a
# CP/CPC chain answers backwards for anything negative. `>` and `<=` had their
# own jump lowerings that skipped the float check entirely, and the operands
# they compared against were registers the arithmetic routine had just used as
# scratch. Negation was wrong the same way: IEEE754 negation is the sign bit,
# not a two's-complement carry chain.
#
# Each line reads pos, neg, zero in that order; sub/subp compare the result of a
# real subtraction (a call, so it cannot fold) against zero.
#
# Expected UART output:
#   FCMP
#   gt=100
#   lt=010
#   ge=101
#   le=011
#   eq=001
#   ne=110
#   sub=0 subp=1
from pymcu.types import uint8
from pymcu.hal.uart import UART


def diff(a: float, b: float) -> float:
    return a - b


def main():
    uart = UART(9600)
    uart.println("FCMP")

    pos: float = 1.5
    neg: float = -2.25
    zero: float = 0.0

    uart.write_str("gt=")
    if pos > 0.0:
        uart.write('1')
    else:
        uart.write('0')
    if neg > 0.0:
        uart.write('1')
    else:
        uart.write('0')
    if zero > 0.0:
        uart.write('1')
    else:
        uart.write('0')
    uart.write(10)

    uart.write_str("lt=")
    if pos < 0.0:
        uart.write('1')
    else:
        uart.write('0')
    if neg < 0.0:
        uart.write('1')
    else:
        uart.write('0')
    if zero < 0.0:
        uart.write('1')
    else:
        uart.write('0')
    uart.write(10)

    uart.write_str("ge=")
    if pos >= 0.0:
        uart.write('1')
    else:
        uart.write('0')
    if neg >= 0.0:
        uart.write('1')
    else:
        uart.write('0')
    if zero >= 0.0:
        uart.write('1')
    else:
        uart.write('0')
    uart.write(10)

    uart.write_str("le=")
    if pos <= 0.0:
        uart.write('1')
    else:
        uart.write('0')
    if neg <= 0.0:
        uart.write('1')
    else:
        uart.write('0')
    if zero <= 0.0:
        uart.write('1')
    else:
        uart.write('0')
    uart.write(10)

    uart.write_str("eq=")
    if pos == 0.0:
        uart.write('1')
    else:
        uart.write('0')
    if neg == 0.0:
        uart.write('1')
    else:
        uart.write('0')
    if zero == 0.0:
        uart.write('1')
    else:
        uart.write('0')
    uart.write(10)

    uart.write_str("ne=")
    if pos != 0.0:
        uart.write('1')
    else:
        uart.write('0')
    if neg != 0.0:
        uart.write('1')
    else:
        uart.write('0')
    if zero != 0.0:
        uart.write('1')
    else:
        uart.write('0')
    uart.write(10)

    down: float = diff(0.04, 0.05)
    up: float = diff(0.05, 0.04)
    uart.write_str("sub=")
    if down > 0.0:
        uart.write('1')
    else:
        uart.write('0')
    uart.write_str(" subp=")
    if up > 0.0:
        uart.write('1')
    else:
        uart.write('0')
    uart.write(10)

    while True:
        pass
