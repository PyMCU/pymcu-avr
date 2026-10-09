# expect: match
# doc: https://docs.pymcu.org/roadmap/
# RFC 0014 family 6: `from pymcu.chips import __CHIP__` binds the fact, and the
# folded value is the same one CPython's shim answers.
from pymcu.chips import __CHIP__

if __CHIP__.name == "atmega328p":
    print("named")
if __CHIP__.arch == "avr":
    print("arch")
print("END")
