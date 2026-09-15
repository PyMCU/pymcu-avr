# Where a printed line's side effects happen (PyMCU#371).
#
# CPython builds the whole line and writes it in one piece, so everything an operand does
# happens before the first character of that line appears. This target streams the text, and
# the literal parts used to go out before the operand between them ran:
#
#   print(f"a={side()}")   gave  a=SIDE\n7      CPython gives  SIDE\na=7
#   print((probe.reading,)) with a raise inside left `(` on the wire, so the handler's own
#                          line started mid-line as `(Retrying!`
#
# Expected output, which is what CPython prints for the same program:
#   SIDE
#   a=7
#   Retrying!
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART
from pymcu.types import uint8


def side() -> uint8:
    print("SIDE")
    return 7


class Probe:
    def __init__(self, broken: uint8) -> None:
        self._broken = broken

    @property
    def reading(self) -> uint8:
        if self._broken == 1:
            raise RuntimeError("Timed out")
        return 42


probe = Probe(1)


def main() -> None:
    uart = UART(9600)
    print(f"a={side()}")
    try:
        print((probe.reading,))
    except RuntimeError:
        print("Retrying!")
    print("done")
    while True:
        pass
