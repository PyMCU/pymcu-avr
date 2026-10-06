# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import uint8
x: uint8 = 255
y: uint8 = 45
print(x + y)
print("END")
