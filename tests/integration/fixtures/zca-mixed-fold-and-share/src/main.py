# RFC 0006, Section 13.2 (docs/rfcs/0006-self-is-this.md): a class with ONE field
# constant across every instance (baud, always 9600) and ONE field that varies
# (pin). Two instances, neither escapes, so RFC 0001's existing outlining already
# shares ONE body across both -- but it shares BOTH fields as runtime parameters,
# never noticing that baud never actually varies. This fixture is the corpus
# entry that lets Phase 3's per-field fold be measured against a concrete byte
# count instead of only reasoned about, and the printed values are exactly what
# the same class, run under CPython, prints -- so a wrong self/field binding
# during the fold (RFC 0001's own #385: "an outlined body received the field as
# a number and self did not exist in it") shows up as a wrong number here, not
# only as a byte-count change.
from pymcu.time import delay_us
from pymcu.types import uint8, uint32


class SoftUart:
    def __init__(self, pin: uint8, baud: uint32):
        self.pin = pin
        self.baud = baud

    def bit_period_us(self) -> uint32:
        return 1000000 // self.baud

    def send_marker(self) -> uint8:
        return self.pin + 1


a = SoftUart(2, 9600)
b = SoftUart(5, 9600)

print(a.bit_period_us())
print(b.bit_period_us())
x: uint8 = a.send_marker()
y: uint8 = b.send_marker()
print(x)
print(y)
print(1 if x == y else 0)

while True:
    delay_us(1000)
