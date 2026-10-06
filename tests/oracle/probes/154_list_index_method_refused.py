# expect: refuse method not supported
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import uint8
xs: list[uint8] = list()
xs.append(2)
xs.append(3)
print(xs.index(3))
print("END")
