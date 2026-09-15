# enum-member-annotation: PyMCU/PyMCU#376.
#
# An annotation naming an enum MEMBER, not the enum type, used to be refused as an unknown
# type -- `digitalio.Direction.OUTPUT` names the value a property actually returns, which is
# unusual Python and legal. `digitalio.Direction` is a plain class with only ALL-CAPS
# attributes (the shape CircuitPython itself uses instead of the `enum` module), and the
# annotation is now read as that value's own type, the same way any other folded constant
# used without a width of its own already is.
#
# Expected UART output: EMA 1 1 END
import digitalio
from pymcu.types import uint8
from pymcu.hal.uart import UART


class ExpanderPin:
    def __init__(self, n: uint8) -> None:
        self._n: uint8 = n

    @property
    def direction(self) -> digitalio.Direction.OUTPUT:
        return digitalio.Direction.OUTPUT


uart = UART(115200)
uart.println("EMA")

p = ExpanderPin(3)
print(p.direction)
if p.direction == digitalio.Direction.OUTPUT:
    print(1)
else:
    print(0)

uart.println("END")

while True:
    pass
