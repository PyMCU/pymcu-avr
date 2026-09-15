# The shape of an Adafruit example, as the bundle ships it (PyMCU#374, #375).
#
# `hcsr04_simpletest.py` builds its sensor AT MODULE LEVEL with a keyword-only default it does
# not pass, and prints a ONE-ELEMENT TUPLE, because the Mu plotter reads a printed tuple. Both
# were refused: the omitted float default was reported as a name the function never received,
# and the tuple as a runtime value the target has no room for.
#
# Neither has to exist at run time. The default is a compile-time float, and the printed tuple
# is text: `(`, the elements with `, ` between them, `)`, with the one-element form keeping the
# trailing comma that is how CPython tells `(12.5,)` from `(12.5)`.
#
# Expected UART output, which is what CPython prints for the same values:
#   (12.5,)
#   (1, 2)
#   0.1
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART


class Sonar:
    def __init__(self, trig: int, echo: int, *, timeout: float = 0.1) -> None:
        self._timeout = timeout
        self._trig = trig

    def hold(self) -> float:
        return self._timeout


sonar = Sonar(5, 2)


def main() -> None:
    uart = UART(9600)
    d: float = 12.5
    print((d,))
    print((1, 2))
    print(sonar.hold())
    print("done")
    while True:
        pass
