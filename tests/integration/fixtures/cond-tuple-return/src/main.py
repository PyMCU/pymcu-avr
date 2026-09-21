# cond-tuple-return: adafruit_neopixel's wheel() shape --
# `return (r, g, b) if ORDER in {RGB, GRB} else (r, g, b, 0)`. The condition is a
# compile-time string membership, so the 3-element arm is the return; the caller
# `s[i] = wheel(pos)` hands the delivered slots to __setitem__ as the sequence
# `val` unpacks, and the bytes must land in the strip's buffer in order.
#
# pos comes from GPIOR0 so the pipeline carries a run-time value end to end --
# wheel's parameter, the tuple slots, the unpack, and the buffer write.
#
# Expected UART output:
#   0
#   2
#   3
#   done
import striplib
from pymcu.hal.console import print
from pymcu.hal.uart import UART
from pymcu.chips.atmega328p import GPIOR0

uart = UART(9600)

s = striplib.Strip(3)
s[0] = striplib.wheel(GPIOR0.value)

print(s.at(0))
print(s.at(1))
print(s.at(2))
print("done")

while True:
    pass
