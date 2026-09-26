# expect: match
# doc: LANGUAGE_ROADMAP.md:79
# An integer literal meeting a float: an operand of a float operation, an argument to a float
# parameter of a real subroutine or an outlined method, and a float literal passed to one.
# The backend loaded a negative literal as its unsigned low byte (-2 as 254), a call handed a
# float parameter the integer's bytes (read as 0.0), and an outlined method rounded a float
# literal argument to an int before passing it.
from pymcu.types import int16
from pymcu.chips.atmega328p import GPIOR0


def half(a: float) -> float:
    return a * 0.5


class P:
    def __init__(self, s: float):
        self.s = s

    def scale(self, a: float) -> float:
        return a * self.s


s = GPIOR0.value
v = float(s) - 3.5
print(v * -2)
print(v + -1)
print(v > -4)
print(-4 < v)
if v > -4:
    print("above")
k: int16 = s - 5
print(half(6))
print(half(-6))
print(half(k))
p = P(2.5)
print(p.scale(-3.0))
print(p.scale(-3))
print(p.scale(k))
print("END")
