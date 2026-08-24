# PyMCU -- literal-width: an unannotated integer costs what the annotated one costs.
#
# Regression for PyMCU#62. `n = 200` inferred int32 regardless of the value and pulled in
# the 32-bit decimal writer: 1100 bytes against 344 for `n: uint8 = 200`, the same value,
# the same program, no diagnostic.
#
# Narrowing is only safe if every assignment is seen, so this fixture pins the VALUES of
# the shapes that must keep working, not just the size:
#   200        -> one literal, narrowed to uint8
#   5 then 300 -> two literals, narrowed to the type that holds both
#   -5         -> signed
#   70000      -> wider than 16 bits
#   200 + 100  -> not literal-only, so it keeps the old inference and must still be right
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)

    a = 200
    uart.print_uint16(a)

    b = 5
    b = 300
    uart.print_uint16(b)

    c = -5
    uart.print_int16(c)

    d = 70000
    uart.print_uint32(d)

    e = 200
    e = e + 100
    uart.print_uint16(e)

    uart.println("done")
