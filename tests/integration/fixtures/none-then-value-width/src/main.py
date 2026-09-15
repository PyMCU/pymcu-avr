# A name gets its WIDTH from the value finally stored in it (PyMCU#385, found by the
# acceptance for it).
#
# Two ways a name was left a byte and truncated whatever was put in it:
#
#   * `x = None` first. None is not a value and emits no store, but the empty binding it
#     left behind was read as a measurement, so the assignment that DID carry a value
#     truncated into a byte. `pulselen = None` ahead of `pulselen = self._echo[0]` is how
#     every CircuitPython driver declares an optional result, and a 570 us echo came back
#     as 58.
#   * a FRESH local inside a body that is expanded rather than compiled once. The binding
#     the fallback hands back is a byte, so a uint16 kept its low byte and a float read
#     back as 0. The same method compiled as a shared subroutine answered correctly, so it
#     showed up only where something forces expansion -- a method reaching through a field,
#     which is what a driver's method does: `timestamp = time.monotonic()` read 0 and every
#     measurement after the first timed out at once.
#
# The value comes from GPIOR0, which reads 0 out of reset, so nothing folds to a constant
# and the store is a real store.
#
# Expected UART output, which is what CPython prints for the same program:
#   fn-none 570
#   fn-plain 570
#   mod-none 570
#   held-none 570
#   held-plain 570
#   held-float 105
#   held-after 570
#   through-wide 570
#   through-float 105
#   END
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import inline, uint8, uint16


@inline
def wide() -> uint16:
    return GPIOR0.value + 570


@inline
def halves() -> float:
    # Halves are exact in binary, so the reading back is about the WIDTH and not
    # about how many digits a float prints in.
    return (GPIOR0.value + 105) * 0.5


def fn_none() -> uint16:
    v = None
    v = wide()
    return v


def fn_plain() -> uint16:
    v = wide()
    return v


class Inner:
    def __init__(self, n: uint8):
        self._n: uint8 = n

    def get(self) -> uint8:
        return self._n


class Held:
    # A field holding an instance forces every method that touches it to be expanded at its
    # call sites, which is the shape the fresh-local width bug needs.
    def __init__(self, n: uint8):
        self._pad: uint8 = 7
        self._inner = Inner(n)

    def none_then_wide(self) -> uint16:
        v = None
        v = wide()
        return v

    def plain_wide(self) -> uint16:
        v = wide()
        return v

    def plain_float(self) -> float:
        v = halves()
        return v

    def wide_then_none(self) -> uint16:
        v = wide()
        unused = None
        return v

    # The two below carry no None at all: reaching THROUGH the field is what forces the body
    # to be expanded, and a fresh local was bound a byte wide there on its own.
    def through_field_wide(self) -> uint16:
        self._inner.get()
        v = wide()
        return v

    def through_field_float(self) -> float:
        self._inner.get()
        v = halves()
        return v


mod_v = None
mod_v = wide()
h = Held(1)


def main() -> None:
    print("fn-none", fn_none())
    print("fn-plain", fn_plain())
    print("mod-none", mod_v)
    print("held-none", h.none_then_wide())
    print("held-plain", h.plain_wide())
    print("held-float", int(h.plain_float() * 2))
    print("held-after", h.wide_then_none())
    print("through-wide", h.through_field_wide())
    print("through-float", int(h.through_field_float() * 2))
    print("END")
    while True:
        pass
