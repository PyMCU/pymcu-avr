# expect: refuse the quotient can exceed int32
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# divmod(-2147483648, b): the literal was classified as int16 because it is
# negative, so the pair never reached the quotient-overflow refusal and the
# firmware wrapped the answer to -2147483648 where CPython answers 2147483648.
# No wider PyMCU integer exists, so the pair refuses at compile time.
from pymcu.types import int8
from pymcu.chips.atmega328p import GPIOR0

b: int8 = int8(GPIOR0.value) - 1

q, r = divmod(-2147483648, b)
print(q)
print(r)
print("END")
