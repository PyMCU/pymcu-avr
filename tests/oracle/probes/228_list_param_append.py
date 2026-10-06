# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.types import uint8

def grow(ys: list[uint8], v: uint8) -> None:
    ys.append(v)

xs: list[uint8] = list()
xs.append(4)
grow(xs, 9)
grow(xs, 7)
print(xs[0])
print(xs[1])
print(xs[2])
print("END")
