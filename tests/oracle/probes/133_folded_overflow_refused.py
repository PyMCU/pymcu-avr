# expect: refuse is out of range for uint8
# doc: https://docs.pymcu.org/language-reference/#type-casts
from pymcu.types import uint8
y: uint8 = 200 + 100
print(y)
print("END")
