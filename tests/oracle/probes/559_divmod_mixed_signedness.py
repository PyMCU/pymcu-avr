# expect: match
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# divmod(uint8, int8) typed the result pair from the LEFT operand: uint8 won the
# width tie and the quotient -4 was stored as 252. The result type now sizes for
# what the division produces -- a signed divisor makes the quotient signed with
# room for -a, and the remainder takes the divisor's sign.
from pymcu.types import uint8, int8
from pymcu.chips.atmega328p import GPIOR0

a: uint8 = 17 + GPIOR0.value
b: int8 = -5 - int8(GPIOR0.value)

print(divmod(a, b))
print(divmod(a, -b))
print("END")
