# PyMCU -- bytearray-call-arg: bytearray() written INLINE as a call argument (PyMCU#380).
#
# f(bytearray([...])) refused with the same "unsupported Python builtin" message the field
# target did (#392): the argument-evaluation loop for a plain (outlined) function call falls
# to VisitExpression on anything that is not a known-array VariableExpr, and VisitCall has no
# lowering for the bytearray() builtin. Every CircuitPython busio.SPI.write()/I2C example
# writes its buffer exactly this way (`spi.write(bytearray([0x9F, 0x00]))`); the assign-first
# rewrite the old diagnostic asked for is not what upstream examples write.
#
# `show` is deliberately NOT @inline: the bug was specific to an outlined call's argument
# list, not to the (already-working) @inline parameter-binding path.
#
# Expected UART output (CPython prints the literal's own elements):
#   10
#   20
#   30
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART


def show(b) -> None:
    print(b[0])
    print(b[1])
    print(b[2])


uart = UART(9600)
show(bytearray([10, 20, 30]))
print("done")

while True:
    pass
