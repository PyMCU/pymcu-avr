# expect: refuse the quotient can exceed int32
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# divmod(int32, int8) sized the pair by clamping the needed rank to int32, so
# INT32_MIN / -1 wrapped to -2147483648 where CPython answers 2147483648. No
# wider PyMCU integer exists, so a pair the quotient cannot fit refuses at
# compile time instead of emitting a wrong value. The dividend below can reach
# INT32_MIN and the divisor's range includes -1.
from pymcu.types import int8, int32
from pymcu.chips.atmega328p import GPIOR0, GPIOR1

a: int32 = int32(GPIOR0.value) - 2147483647 - 1
b: int8 = int8(GPIOR1.value) - 1

q, r = divmod(a, b)
print(q)
print(r)
print("END")
