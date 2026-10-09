# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `import pymcu.chips as chips` reads the same facts off the module object.
import pymcu.chips as chips

if chips.__CHIP__.name == "atmega328p":
    print("mod")
if chips.__FREQ__ == 16000000:
    print("freq")
print("END")
