# expect: refuse creates H aliases of ONE row object
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import uint8, uint16

n: uint16 = 3
rep: list[list[uint8]] = [[0, 0]] * n
print(len(rep))
