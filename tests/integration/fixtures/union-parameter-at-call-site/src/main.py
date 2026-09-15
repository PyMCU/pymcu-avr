# PyMCU -- union-parameter-at-call-site (PyMCU#442): `Union[A, B]` on a parameter of an
# @inline-expanded function or method (every constructor included -- a ZCA instance always
# builds at its call site).
#
# `Union[A, B]` used to be refused outright, on the grounds that a real subroutine has one
# ABI and nowhere to resolve which member a given call means. That is true of a real
# subroutine and false of a constructor: the argument's actual type is known at each call
# site, exactly the way an @inline overload already dispatches on an argument's type. Found
# reducing three Adafruit libraries, all on a constructor parameter:
#
#   adafruit_character_lcd  red: Union[pwmio.PWMOut, digitalio.DigitalInOut]
#   adafruit_debouncer      io_or_predicate: Union[ROValueIO, Callable[[], bool]]
#   adafruit_ht16k33        address: Union[int, List[int], Tuple[int, ...]] = 0x70
#
# `debounce_marker` covers the third shape (a class vs a plain function reference through the
# SAME parameter) by construction succeeding for both, rather than by reading a value back out
# of the bound parameter: PyMCU#446, found while reducing this fixture, is a separate,
# pre-existing bug (nothing to do with Union) where a nested constructor argument's field read
# INSIDE the outer constructor's own body answers 0. holder_a/holder_b and matrix_int/
# matrix_list read a bound value back correctly because they read it from OUTSIDE the
# constructor, at the call site, which #446 does not affect.
#
# Expected UART output:
#   holder_a 5
#   holder_b 7
#   matrix_int 112
#   matrix_list 1
#   debounce_marker 2
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART
from pymcu.types import uint8


class A:
    def __init__(self, x: uint8) -> None:
        self.x: uint8 = x


class B:
    def __init__(self, y: uint8) -> None:
        self.y: uint8 = y


class Holder:
    def __init__(self, thing: Union[A, B]) -> None:
        self.thing = thing


class Matrix:
    def __init__(self, address: Union[uint8, List[uint8], Tuple[uint8, ...]] = 0x70) -> None:
        self.address = address


class ROValueIO:
    def __init__(self, v: uint8) -> None:
        self.value: uint8 = v


def my_predicate() -> uint8:
    return 1


debounce_count: uint8 = 0


class Debouncer:
    def __init__(self, io_or_predicate: Union[ROValueIO, Callable[[], bool]]) -> None:
        global debounce_count
        debounce_count = debounce_count + 1


def holder_a() -> uint8:
    h = Holder(A(5))
    return h.thing.x


def holder_b() -> uint8:
    h = Holder(B(7))
    return h.thing.y


def matrix_int() -> uint8:
    m = Matrix()
    return m.address


def matrix_list() -> uint8:
    m = Matrix([1, 2, 3])
    return m.address[0]


def debounce_marker() -> uint8:
    d1 = Debouncer(ROValueIO(9))
    d2 = Debouncer(my_predicate)
    return debounce_count


uart = UART(9600)

print("holder_a", holder_a())
print("holder_b", holder_b())
print("matrix_int", matrix_int())
print("matrix_list", matrix_list())
print("debounce_marker", debounce_marker())
print("done")

while True:
    pass
