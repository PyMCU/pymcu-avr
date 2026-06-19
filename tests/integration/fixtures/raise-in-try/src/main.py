# raise-in-try: a `raise` lexically inside a `try` body (NOT via a function call)
# must be caught by the enclosing `except` in the SAME function.
#
# Before the fix, such a raise lowered to `SET; RET` (the cross-function error
# epilogue): it returned from the function instead of jumping to the local catch,
# so the except was skipped AND main RET'd off an empty stack. Now it lowers to
# `LDI R22,code; JMP catch` (no SET, no RET) and is caught locally.
#
# Expected UART output:
#   RT
#   A:caught     (direct raise caught locally)
#   B:ok         (no raise -> happy path)
#   C:caught     (raise inside a nested if, still in the try body)
#   DONE
from pymcu.types import uint8
from pymcu.hal.uart import UART
from pymcu.exceptions import ValueError


def main():
    uart = UART(9600)
    uart.println("RT")

    flag: uint8 = 0

    # A: unconditional direct raise in the try body
    try:
        raise ValueError
        uart.println("A:miss")
    except ValueError:
        uart.println("A:caught")

    # B: no raise -> the try body completes, except not triggered
    try:
        if flag == 1:
            raise ValueError
        uart.println("B:ok")
    except ValueError:
        uart.println("B:miss")

    # C: raise nested inside an if inside the try body
    try:
        if flag == 0:
            raise ValueError
        uart.println("C:miss")
    except ValueError:
        uart.println("C:caught")

    uart.println("DONE")
    while True:
        pass
