# union-protocol-member: PyMCU/PyMCU#465.
#
# adafruit_debouncer's Debouncer(pin) with pin a digitalio.DigitalInOut. The
# parameter is Union[ROValueIO, Callable[[], bool]]; ROValueIO is a Protocol
# with a .value property. DigitalInOut is not named ROValueIO, but it has that
# property, and CPython accepts the call. #442 folded Union[A, B] at the call
# site when the argument's class WAS a member; a structural match was still
# refused.
#
# WHAT DISCRIMINATES: 5, the Pin constructor argument, and 2, one construction
# per Union arm. A compile that still required the class name would not build.
# The field is not read back through Debouncer: .value is also the Protocol
# getter spelling, and a nested constructor field read is PyMCU#446.
#
# Expected UART output:
#   5
#   2
#   END
from pymcu.types import uint8

hits: uint8 = 0
captured: uint8 = 0


class ROValueIO(Protocol):
    @property
    def value(self) -> uint8: ...


class Pin:
    def __init__(self, v: uint8) -> None:
        global captured
        captured = v
        self.value: uint8 = v


class Debouncer:
    def __init__(self, io_or_predicate: Union[ROValueIO, Callable[[], bool]]) -> None:
        global hits
        hits = hits + 1
        self._io = io_or_predicate


def my_predicate() -> uint8:
    return 1


def main() -> None:
    Debouncer(Pin(5))
    Debouncer(my_predicate)
    print(captured)
    print(hits)
    print("END")
    while True:
        pass
