# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# The negative case for PyMCU golperf's proven-power-of-two mod rewrite: a
# divisor the range analysis does NOT get to narrow -- one that is provably
# constant but NOT a power of two (10), and one that is never provably a
# single value at all (a genuine runtime parameter) -- must still lower to a
# correct, if unoptimized, floor modulo. Rewriting `a % 10` to an AND would be
# silently wrong (`-1 & 9` is 9, not CPython's `-1 % 10 == 9`... coincidence
# at -1, but `-11 % 10` is 9 too while `-11 & 9` is 5): this probe exists so
# that coincidence is not mistaken for correctness at more values.
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import int16, uint8


def mod_by(a: int16, b: uint8) -> int16:
    return a % b


x: int16 = GPIOR0.value

print((x - 1) % 10)
print((x - 11) % 10)
print((x - 100) % 10)
print(mod_by(x - 1, 7))
print(mod_by(x - 1, 13))
print("END")
