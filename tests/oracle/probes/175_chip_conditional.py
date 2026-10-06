# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.chips import __CHIP__
if __CHIP__.name == "atmega328p":
    print("avr")
else:
    print("other")
print("END")
