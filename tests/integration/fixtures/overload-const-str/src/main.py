# overload-const-str: picking an overload looks at the argument TYPES.
#
# The registered key spells parameter types raw ("const[str]") and the call site
# spells them normalized ("str"), so for a const[...] parameter the exact lookup
# never hits and every such call fell through to "first overload with the right
# arity" -- declaration order. With the integer overloads declared first, as in
# machine.Pin, a string argument selected the integer one: Pin("RA4", Pin.OUT)
# on PIC died telling the caller to pass a port name, which is what it passed.
#
# Each __init__ tags itself, so the tag names the overload that ran. The integer
# ones are declared first on purpose: that is the order that used to lose.
#
# Expected UART output:
#   i2=1 i3=2
#   s2=3 s3=4
from pymcu.types import uint8, const, inline
from pymcu.hal.uart import UART


class Probe:
    @inline
    def __init__(self, pin_id: const[uint8], mode: const[uint8] = 1):
        self._tag = 1

    @inline
    def __init__(self, pin_id: const[uint8], mode: const[uint8], pull: const[uint8]):
        self._tag = 2

    @inline
    def __init__(self, pin_id: const[str], mode: const[uint8] = 1):
        self._tag = 3

    @inline
    def __init__(self, pin_id: const[str], mode: const[uint8], pull: const[uint8]):
        self._tag = 4

    @inline
    def tag(self) -> uint8:
        return self._tag


def main():
    uart = UART(9600)

    i2 = Probe(13, 0)
    i3 = Probe(13, 0, 0)
    s2 = Probe("PC0", 0)
    s3 = Probe("PC0", 0, 0)

    print(f"i2={i2.tag()} i3={i3.tag()}")
    print(f"s2={s2.tag()} s3={s3.tag()}")

    while True:
        pass
