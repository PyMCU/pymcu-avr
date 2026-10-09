# millis-long-sleep-accuracy: millis() across a real 1 s sleep().
#
# millis_init() + millis() only counted COMPLETE Timer0 overflows (each
# ~1.024 ms): a single reading could lag true elapsed time by almost one
# whole overflow period. The ISR's fractional correction (landed earlier)
# fixes the LONG-RUN average rate, but a single before/after reading still
# lands short unless millis() itself also folds in the current, in-progress
# overflow's progress -- which is what this measures. Worked example: after
# exactly 1 s, 976 overflows have completed (976 * 1.024 ms = 999.424 ms);
# reading only the completed count gives 999, a 0.1% error. Folding in
# TCNT0's progress through the 977th (in-progress) overflow plus the
# ISR's not-yet-rounded fractional carry recovers the missing 0.576 ms,
# landing on the true 1000.
#
# GPIOR0/GPIOR1 hold millis() - millis() (low/high byte): elapsed ms across
# sleep(1.0), compared against sleep()'s own cycle-exact 1000 ms in the test.
from pymcu.types import uint8, uint32, asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1
from pymcu.time import millis_init, millis, sleep


def main():
    millis_init()
    t0: uint32 = millis()
    sleep(1.0)
    t1: uint32 = millis()
    elapsed: uint32 = t1 - t0
    GPIOR0.value = uint8(elapsed & 0xFF)
    GPIOR1.value = uint8((elapsed >> 8) & 0xFF)
    asm("BREAK")
    while True:
        pass
