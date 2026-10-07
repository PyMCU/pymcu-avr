# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Same shape as 624, calls reversed: if a divisor's proven range were allowed
# to leak from one call site into the shared body, reversing which call runs
# last would flip which answer gets corrupted (5, 5 instead of 1, 1) --
# proof that neither call site's argument may specialize the shared function.
from pymcu.types import int16, uint16


def rem(x: int16, n: uint16) -> int16:
    return x % n


print(rem(5, 4))
print(rem(5, 32))
print("END")
