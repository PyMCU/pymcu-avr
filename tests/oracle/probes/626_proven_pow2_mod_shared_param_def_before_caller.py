# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Same shape as 624/625, with the callee defined BEFORE the caller (so the
# shared body is generated before either call's argument-binding copy runs)
# and the caller named main instead of a plain top-level statement. Covers
# the definition-order variant the review named as a risk alongside the
# original ordering.
from pymcu.types import int16, uint16


def rem(x: int16, n: uint16) -> int16:
    return x % n


def main():
    print(rem(5, 32))
    print(rem(5, 4))


main()
print("END")
