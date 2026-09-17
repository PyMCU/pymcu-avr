# PyMCU -- bytearray-method-call-arg: bytearray() written INLINE as a METHOD call
# argument (PyMCU#459).
#
# `f(bytearray([...]))` on a plain function was fixed under #380, but the same buffer
# spelled as the argument of a method call -- `d.write(bytearray([0x9F, 0x00]))`, the
# exact shape every CircuitPython busio.SPI/I2C example uses -- still refused with
# "bytearray() is a Python builtin that PyMCU does not provide": the outlined-method
# argument loop evaluates each arg with VisitExpression directly, which has no
# lowering for the bytearray() builtin. Both call shapes are pinned: the list
# literal and bytearray(N), which must reach the callee zero-filled.
#
# `Dev.write` expands @inline like every undecorated method, which is exactly where
# the refusal lived: EmitInlineFunctionCall bound every non-literal argument through
# VisitExpression, and VisitCall has no lowering for the bytearray() builtin. The
# outlined-method argument loops had the same gap and are covered by the same fix.
#
# Expected UART output (CPython's answer for the same calls):
#   10
#   20
#   0
#   0
#   3
#   4
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART


class Dev:
    def write(self, buf: bytearray) -> None:
        print(buf[0])
        print(buf[1])


uart = UART(9600)
d = Dev()
d.write(bytearray([10, 20]))
d.write(bytearray(2))
d.write(buf=bytearray([3, 4]))
print("done")

while True:
    pass
