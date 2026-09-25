# expect: match
# doc: docs/language/roadmap.md:90
from pymcu.chips import __CHIP__
if __CHIP__.name == "atmega328p":
    print("avr")
else:
    print("other")
print("END")
