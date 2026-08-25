# PyMCU -- inline-multi-return: an @inline with several returns is a run-time value, and the
# store that consumes it is emitted.
#
# Regression for PyMCU#132. The result of an @inline expansion was tracked as the constant of
# its FIRST return, so when different run-time branches returned different constants the whole
# call folded to the first one. Every consumer that folds a constant right-hand side then
# emitted NOTHING: `obj.field = helper(x)` had no store instruction anywhere in the function,
# and `inline_callee(helper(x))` bound its parameter to the first return. The field kept
# whatever the backend last left in the register, which is why the answer read as "the first
# return" rather than as garbage. Clean build, no diagnostic.
#
# What it cost: PWM.set_freq is written exactly that way,
#
#     self._start_val = pwm_prescaler_for_freq(self._pin, freq)
#     self._tccr_b.value = self._start_val
#
# so it programmed prescaler 1 (62.5 kHz) for every frequency below 22 kHz. A wrong PWM
# frequency on real hardware with nothing reported. The pattern `self._field = <@inline
# helper>(...)` is how the whole ZCA HAL stores a computed setting.
#
# GPIOR0 and GPIOR1 carry the seed, high byte then low, and the TEST writes them: a literal
# frequency would fold the selecting branch away and measure the constant folder instead of
# the multi-return path. Note that qemu does not retain a write to GPIOR0, so this fixture is
# only meaningful under the avr8sharp harness that seeds Data[0x3E] and Data[0x4A].
#
# `expr` is the control: adding `+ 0` made the same call correct before the fix, because an
# arithmetic consumer does not fold a constant right-hand side the way a field store does.
#
# Expected, per seed. The last column is what the UNFIXED compiler produced, because the shape
# of that column is the point:
#
#   seed f   GPIOR0:GPIOR1   direct  expr  arg   TCCR0B   unfixed
#   60       0:60            5       5     5     0x05     direct=1 arg=1 TCCR0B=0x01
#   260      1:4             5       5     5     0x04     direct=1 arg=1 TCCR0B=0x01
#   4000     15:160          2       2     2     0x02     direct=1 arg=1 TCCR0B=0x01
#   25600    100:0           1       1     1     0x01     PASSES EVEN WITH THE BUG
#
# READ THE 25600 ROW BEFORE CHANGING THIS FIXTURE. The defect folds every call to the FIRST
# `return`, which is 1, and at 25600 the correct answer is also 1. So that seed is green over a
# fully live bug. A fixture built on one badly chosen seed would have certified this defect as
# fixed. That is why there are four rows and not one, and why any seed added here has to be
# checked against "what would the first return have given".
#
# 260 is worth keeping for the opposite reason: plain() says 5 where the HAL says 0x04, so the
# helper's own value and the prescaler cannot be mistaken for one another.
from pymcu.chips.atmega328p import GPIOR0, GPIOR1
from pymcu.hal.pwm import PWM
from pymcu.hal.uart import UART
from pymcu.types import uint8, uint16, inline


@inline
def plain(freq: uint16) -> uint8:
    if freq > 22097:
        return 1
    elif freq > 2762:
        return 2
    elif freq > 488:
        return 3
    else:
        return 5


class Box:
    def __init__(self):
        self._a: uint8 = 0
        self._b: uint8 = 0


def main():
    u = UART(9600)
    a = PWM("PD6", 128, 1000)
    f: uint16 = uint16(GPIOR0.value) * 256 + uint16(GPIOR1.value)

    box = Box()
    box._a = plain(f)       # bare call as the right-hand side of a field store
    box._b = plain(f) + 0   # the control: same call inside an expression

    u.write_str("direct=")
    u.print_byte(box._a)
    u.write_str("expr=")
    u.print_byte(box._b)
    u.write_str("arg=")
    u.print_byte(plain(f))  # bare call as the argument of another @inline

    # The HAL method the issue was found through. Its own store is the same shape, and
    # TCCR0B is what the hardware ends up running at.
    a.set_freq(f)

    u.write_str("done\n")
    while True:
        pass


main()
