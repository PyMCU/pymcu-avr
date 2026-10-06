# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.collections import FixedDict
d = FixedDict(4)
d[1] = 7
d[2] = 9
print(len(d))
print(d[1])
print(d.get(3, 5))
print("END")
