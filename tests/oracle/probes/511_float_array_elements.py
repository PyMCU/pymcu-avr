# expect: match
# doc: docs/language/limitations.md:53
# A float array element is four bytes in the float register layout. Same defect as 510: the
# element load and store moved two bytes, and a field array of floats did not assemble.
from pymcu.types import uint8, inline
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

f: float[3] = [0.0] * 3
f[s] = s + 1234.5
f[s + 2] = s - 0.25
f[s + 2] += s + 1.0
print(f[s], f[1], f[s + 2], f[-1])


class Acc:
    def __init__(self):
        self.v: float[70] = [0.0] * 70

    def put(self, i: uint8, x: float):
        self.v[i] = x

    def get(self, i: uint8) -> float:
        return self.v[i]


a = Acc()
a.put(s + 69, s + 2.5)
a.v[s + 68] = s - 1.25
print(a.get(s + 69), a.v[s + 68], a.v[s])
print("END")
