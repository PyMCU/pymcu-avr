# expect: refuse must be compile-time integer constants
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# A list a constructor was handed is walked at compile time, one copy of the body per element,
# so its elements have to be known then. One that holds a value only the running program knows
# is refused in words, not walked as if the constant it was built from were the value.
from pymcu.chips.atmega328p import GPIOR0


class Frame:
    def __init__(self, header: list[int]):
        self.header = header

    def weigh(self) -> int:
        total = 0
        for i, v in enumerate(self.header):
            total += v * (i + 1)
        return total


x = GPIOR0.value
f = Frame([x, 4, x + 3])
print(f.weigh())
print("END")
