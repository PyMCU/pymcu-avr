# PyMCU -- range-bound-to-a-name: a range() given a name and used only as an iterable.
#
# PyMCU#363. A range whose bounds fold was refused the moment it was given a name, with
# "range() is not a value in PyMCU: use it as the iterable of a for loop, in 'x in range(...)',
# in reversed(range(...)) or in enumerate(range(...))" -- on a program whose only use of the
# name is the for loop the message asks for. `reversed(order)`, a form the message itself
# offers, was refused on the same line.
#
# A range bound to a name is not a value; it is a compile-time sequence, which the compiler
# already has a representation for. A name that reaches a position needing a value is still
# refused, and that refusal now names the variable.
#
# This is how adafruit_register.i2c_bits picks a byte order: one name, two branches, one loop.
#
#   buf = 00 12 34 56
#   lsb.walk()   reads index 2 then 1   ->  52, 18
#   msb.walk()   reads index 1 then 2   ->  18, 52
#   for i in order        over [1, 2]   ->  18, 52
#   for i in reversed(order)            ->  52, 18
buf = bytearray([0x00, 0x12, 0x34, 0x56])


class Field:
    def __init__(self, width: int, lsb_first: bool) -> None:
        self.width = width
        self.lsb_first = lsb_first

    def walk(self) -> None:
        order = range(self.width, 0, -1)
        if not self.lsb_first:
            order = reversed(order)
        for i in order:
            print(buf[i])


lsb = Field(2, True)
msb = Field(2, False)


def main() -> None:
    lsb.walk()
    msb.walk()

    order = range(1, 3)
    for i in order:
        print(buf[i])
    for i in reversed(order):
        print(buf[i])

    print("done")
