# class-object-call: a method with no `self`, called through the class (PyMCU/PyMCU#201).
#
# `Math.double(21)` used to emit a call to `Math_double` into a build that defines no such
# function, so it failed at avr-ld with a symbol and a byte offset and no line of the program
# in it. The method is compiled as an ordinary function under the class prefix now, which is
# the name the call site was already forming.
#
# `Math` carries a field and an ordinary instance method on purpose: the decision that used to
# send the method to expansion-only turns on the field layout, so a class that HAS one is the
# harder case, not the easier one.
#
# The three numbers are distinct and none is a plausible answer to another line: 42 needs the
# argument to arrive, 18 needs the receiver NOT to consume it, and 7 needs the ordinary method
# to still read its own field.
#
# Expected UART output:
#   CO
#   42
#   18
#   7
#   END
from pymcu.hal.uart import UART
from pymcu.types import uint8


class Math:
    def __init__(self):
        self.base: uint8 = 3

    @staticmethod
    def double(x: uint8) -> uint8:
        return x + x

    def plus_base(self, x: uint8) -> uint8:
        return x + self.base


uart = UART(115200)
print("CO")
print(Math.double(21))
m = Math()
print(m.double(9))
print(m.plus_base(4))
print("END")

while True:
    pass
