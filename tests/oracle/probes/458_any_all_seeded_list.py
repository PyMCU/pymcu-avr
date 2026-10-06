# expect: match
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# any() and all() over list literals of run-time elements, read through a branch so the
# bool's printed form does not enter. Probe 057 folds literals. 256 is truthy with a zero
# low byte: an element tested on eight bits turns the third and fourth answers around.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
if any([s, s, s + 5]):
    print("any-a yes")
else:
    print("any-a no")
if all([s + 1, s, s + 3]):
    print("all-a yes")
else:
    print("all-a no")
if any([s, s + 256]):
    print("any-b yes")
else:
    print("any-b no")
if all([s + 256, s + 1]):
    print("all-b yes")
else:
    print("all-b no")
if any([s, s]):
    print("any-c yes")
else:
    print("any-c no")
print("END")
