# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.types import uint8

def total(xs: list[uint8], n: uint8) -> uint8:
    s = 0
    for i in range(n):
        s = s + xs[i]
    return s

xs: list[uint8] = list()
xs.append(4)
xs.append(5)
xs.append(6)
print(total(xs, 3))
print("END")
