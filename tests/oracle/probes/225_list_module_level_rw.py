# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.types import uint8

xs: list[uint8] = list()
xs.append(4)
xs.append(5)
xs.append(6)
print(xs[0])
print(xs[1])
print(xs[2])
print("END")
