# ATmega328P: unannotated locals assigned from folded constant expressions.
#
# A numeric expression folded to a constant at compile time carried no type,
# so the variable landed on the UINT8 default and truncated: `x = -7` (unary
# minus over a literal is not itself an IntegerLiteral) stored 249, and
# `x = 1 << 9` stored 0. Plain literals (`x = 300`) already worked; every
# folded expression must behave the same as the literal of its value.
#
# Expected UART output (9600 baud), one value per line via print_int16/print_uint16:
#   -7      x = -7
#   -300    x = -300
#   -7      x = 0 - 7
#   400     x = 100 * 4
#   512     x = 1 << 9
#   300     x = 200 + 100
#   -10     x = 10 - 20
#   -7      x = -(3 + 4)
#   256     x = 255 + 1
#   333     x = 1000 // 3
#   -15     x = 5 * -3
#   D       done marker
from pymcu.hal.uart import UART
from pymcu.time import delay_ms


def main():
    uart = UART(9600)

    a = -7
    uart.print_int16(a)
    b = -300
    uart.print_int16(b)
    c = 0 - 7
    uart.print_int16(c)
    d = 100 * 4
    uart.print_int16(d)
    e = 1 << 9
    uart.print_int16(e)
    f = 200 + 100
    uart.print_int16(f)
    g = 10 - 20
    uart.print_int16(g)
    h = -(3 + 4)
    uart.print_int16(h)
    i = 255 + 1
    uart.print_int16(i)
    j = 1000 // 3
    uart.print_int16(j)
    k = 5 * -3
    uart.print_int16(k)

    uart.write('D')

    while True:
        delay_ms(1000)
