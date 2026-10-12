# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""A bytearray of some length and a str are true when they are not empty."""
from pymcu.types import uint8

b2 = bytearray(2)
if b2:
    print("b2 true")
else:
    print("b2 false")
s0 = ""
s1 = "x"
if s0:
    print("s0 true")
else:
    print("s0 false")
if s1:
    print("s1 true")
else:
    print("s1 false")
print("END")
