# expect: match
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# chr() of a run-time code point bound to a name prints its character, as the same call
# written into print() (probe 455) and chr() of a literal bound to a name do. It printed the
# code point, 72. A name that chr() never binds is the control and stays a number.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


def show(n: uint8):
    e = chr(n + 33)
    print(e)


s = GPIOR0.value
d = chr(72)
print(d)
c = chr(s + 72)
print(c)
show(s)
n = s + 72
print(n)
print("END")
