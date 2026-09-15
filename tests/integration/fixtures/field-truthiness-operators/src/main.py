# Every OPERATOR that asks a field for a truth value asks its class (PyMCU#385).
#
# fixtures/instance-field-truthiness covers `if` and `while not` on a field. The rewrite is
# reached from four more places -- `not`, the operands of `and` and `or`, a conditional
# expression and `bool()` -- and each had its own way of missing it. The conditional
# expression missed it for a reason of its own: it asked whether the condition was None
# BEFORE rewriting, and a field whose class collapsed onto the field below it has no value
# under its own name, so it read as None and the expression answered with its false side
# without ever reaching the class.
#
# Every class here answers the OPPOSITE of the scalar it collapses to: Inv(0) stores 0 and is
# TRUE, Inv(1) stores 1 and is FALSE. A condition decided by storage and a condition decided
# by the class therefore never agree, so no line can be right by accident. The seed comes from
# GPIOR0, which reads 0 out of reset, so nothing folds to a constant either.
#
# Expected UART output, which is what CPython prints for the same program:
#   if-true 1
#   if-false 0
#   not 1
#   and 1
#   or 1
#   ternary 1
#   bool 1
#   bool-dunder 1
#   name 1
#   name-bool 1
#   late 1
#   wait 2
#   wait 0
#   END
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


class Inv:
    # Truth by __len__, inverted: the object is true exactly when its byte is zero.
    def __init__(self, n: uint8):
        self._n: uint8 = n

    def __len__(self) -> uint8:
        if self._n == 0:
            return 1
        return 0


class Both:
    # Defines both dunders and they disagree. Python asks __bool__ first, so the answer is
    # the inverted one; falling back to __len__ would print 0 here.
    def __init__(self, a: uint8):
        self._a: uint8 = a

    def __bool__(self) -> bool:
        return self._a == 0

    def __len__(self) -> uint8:
        return 0


class Counter:
    # A __len__ that counts its own calls and answers 0, 0, then 1. The loop below ends only
    # if the condition is re-read every turn, so the turn count measures the re-evaluation.
    def __init__(self, start: uint8):
        self._calls: uint8 = start

    def __len__(self) -> uint8:
        self._calls = self._calls + 1
        if self._calls < 3:
            return 0
        return 1


class Owner:
    # A scalar field in front of the instance ones, so the owner is not a single-field handle.
    def __init__(self, seed: uint8):
        self._pad: uint8 = 7
        self._true = Inv(seed)
        self._false = Inv(seed + 1)
        self._both = Both(seed)
        self._counter = Counter(seed)

    def if_true(self) -> uint8:
        if self._true:
            return 1
        return 0

    def if_false(self) -> uint8:
        if self._false:
            return 1
        return 0

    def not_false(self) -> uint8:
        if not self._false:
            return 1
        return 0

    def and_both(self) -> uint8:
        if self._true and self._both:
            return 1
        return 0

    def or_either(self) -> uint8:
        if self._false or self._true:
            return 1
        return 0

    def ternary(self) -> uint8:
        return 1 if self._true else 0

    def bool_field(self) -> uint8:
        if bool(self._true):
            return 1
        return 0

    def bool_dunder(self) -> uint8:
        if self._both:
            return 1
        return 0

    def wait(self) -> uint8:
        turns: uint8 = 0
        while not self._counter:
            turns = turns + 1
        return turns


class Late:
    # The field's class is written BELOW this one, which is legal Python and is how a module
    # that groups its public class first is laid out. The scan reaches this owner before it
    # has ever seen the class, so a check that asks whether the name is a known class answers
    # no and the condition goes back to being decided by storage -- silently.
    def __init__(self, seed: uint8):
        self._pad: uint8 = 7
        self._late = LateInv(seed)

    def truth(self) -> uint8:
        if self._late:
            return 1
        return 0


class LateInv:
    def __init__(self, n: uint8):
        self._n: uint8 = n

    def __len__(self) -> uint8:
        if self._n == 0:
            return 1
        return 0


seed: uint8 = GPIOR0.value
o = Owner(seed)
n = Inv(seed)
late = Late(seed)


def main() -> None:
    print("if-true", o.if_true())
    print("if-false", o.if_false())
    print("not", o.not_false())
    print("and", o.and_both())
    print("or", o.or_either())
    print("ternary", o.ternary())
    print("bool", o.bool_field())
    print("bool-dunder", o.bool_dunder())
    print("name", 1 if n else 0)
    print("name-bool", 1 if bool(n) else 0)
    print("late", late.truth())
    print("wait", o.wait())
    print("wait", o.wait())
    print("END")
    while True:
        pass
