# expect: match
# doc: docs/language/roadmap.md
import array
from pymcu.types import uint8

def total(xs: list[uint8], n: uint8) -> uint8:
    s = 0
    for i in range(n):
        s = s + xs[i]
    return s

a = array.array("B", [4, 5, 6])
print(total(a, 3))
print("END")
