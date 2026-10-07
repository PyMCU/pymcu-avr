# expect: match
# doc: https://docs.pymcu.org/limitations/
# The wrapper probes' control: a ternary whose arms answer scalars must keep
# compiling -- the refusal reads the class off the evaluated value, so a
# uint8 answer through the same TernaryExpr spelling stays legal.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


def f(flag: bool):
    return (1 if flag else 2), 7


flag = GPIOR0.value == 0
a, b = f(flag)
print(a + b)
print("END")
