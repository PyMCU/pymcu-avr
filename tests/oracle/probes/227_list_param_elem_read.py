# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.types import uint8

def head2(ys: list[uint8]) -> uint8:
    return ys[1]

xs: list[uint8] = list()
xs.append(4)
xs.append(5)
xs.append(6)
print(head2(xs))
print("END")
