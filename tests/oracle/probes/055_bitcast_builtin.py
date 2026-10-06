# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import bitcast, uint32
print(bitcast(uint32, 1.0))
print("END")
