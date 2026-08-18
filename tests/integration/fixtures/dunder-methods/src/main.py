# dunder-methods: the operator protocol on a user class.
#
# Supported here: __len__ (len), __getitem__/__setitem__ (indexing),
# __contains__ (in), __eq__/__lt__ (comparison), __add__ (operator
# overloading), __call__ (a callable instance), __bool__ (truthiness) and
# __enter__/__exit__ (with, including the value `as` binds).
#
# One divergence from CPython, deliberate and shared with every comparison in
# PyMCU: `in`, `==` and `<` yield the integer the dunder returned, so they print
# 1/0 where CPython prints True/False.
#
# Not supported (each one a compile error, never a wrong number):
# interpolating an instance (no runtime __str__), the __iter__/__next__ loop
# protocol, and truthiness of a class defining neither __bool__ nor __len__.
#
# Expected UART output:
#   DUN
#   len=3 idx=5 in=1
#   eq=0 lt=1 add=7 call=5
#   set=8
#   f0 t1
#   with=3
#   END
from pymcu.types import uint8
from pymcu.hal.uart import UART


class Box:
    def __init__(self, n: uint8):
        self.n = n

    def __len__(self) -> uint8:
        return self.n

    def __getitem__(self, i: uint8) -> uint8:
        return self.n + i

    def __contains__(self, v: uint8) -> uint8:
        return v == self.n

    def __eq__(self, other) -> uint8:
        return self.n == other.n

    def __lt__(self, other) -> uint8:
        return self.n < other.n

    def __add__(self, other) -> uint8:
        return self.n + other.n

    def __call__(self, i: uint8) -> uint8:
        return self.n + i

    def __bool__(self) -> uint8:
        return self.n

    def __enter__(self) -> uint8:
        return self.n

    def __exit__(self, a: uint8, b: uint8, c: uint8):
        self.n = 0


class Cell:
    def __init__(self, n: uint8):
        self.n = n

    def __setitem__(self, i: uint8, v: uint8):
        self.n = v + i

    def __getitem__(self, i: uint8) -> uint8:
        return self.n


def main():
    uart = UART(9600)
    uart.println("DUN")

    b = Box(3)
    c = Box(4)
    print(f"len={len(b)} idx={b[2]} in={3 in b}")
    print(f"eq={b == c} lt={b < c} add={b + c} call={b(2)}")

    cell = Cell(3)
    cell[1] = 7
    print(f"set={cell[0]}")

    zero = Box(0)
    if zero:
        uart.write_str("t0")
    else:
        uart.write_str("f0")
    if b:
        uart.write_str(" t1")
    uart.write(10)

    with b as v:
        print(f"with={v}")

    uart.println("END")

    while True:
        pass
