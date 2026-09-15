# PyMCU -- bytes-literal-arg: `bytes([...])` / `bytes(N)` written as a call argument
# (PyMCU#431).
#
# `bytearray(...)` written the same way already had two recognizers: one that normalises it
# to a ListExpr for an @inline callee's UNANNOTATED parameter, so `for x in param` unrolls
# (adafruit_bus_device's `bus_device.write(bytes([A_DEVICE_REGISTER]))`, where `write` is
# `for i, b in enumerate(buffer): ...`), and one that materialises a hidden fixed buffer for
# a REGULAR callee's `bytearray`/`bytes` parameter. Neither looked for the callee name
# `bytes`, only `bytearray` -- so `bytes([...])` fell through both and reached the generic
# call-expression visitor, which has no lowering for the `bytes` builtin and reported
# "bytes() is a Python builtin that PyMCU does not provide", which is false: a `bytes`
# PARAMETER is already read as the exact buffer a `bytearray` parameter is.
#
# Expected UART output (CPython's numbers for the same bytes):
#   inline_lit 18
#   inline_zero 0
#   outline_first 5
#   outline_third 7
#   local_lit 6
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART
from pymcu.types import inline, uint8


@inline
def sum_inline(buf) -> uint8:
    s: uint8 = 0
    for b in buf:
        s = s + b
    return s


def first_outline(buf: bytes) -> uint8:
    return buf[0]


def third_outline(buf: bytes) -> uint8:
    return buf[2]


uart = UART(9600)

print("inline_lit", sum_inline(bytes([5, 6, 7])))
print("inline_zero", sum_inline(bytes(3)))
print("outline_first", first_outline(bytes([5, 6, 7])))
print("outline_third", third_outline(bytes([5, 6, 7])))

local_buf: bytes = bytes([1, 2, 3])
print("local_lit", local_buf[0] + local_buf[1] + local_buf[2])

print("done")

while True:
    pass
