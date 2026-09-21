# factory-pin-list-loop: the list form of PyMCU/PyMCU#421 -- adafruit_pcf8574's
# `pins = [pcf.get_pin(i) for i in range(8)]` followed by
# `for p in pins: p.switch_to_output(value=False)`. Each comprehension element is
# a factory call's result, and the unrolled loop has to carry that returned class
# into p -- without it the call mangled to an undefined `p_switch_to_output`.
#
# The discriminating output is each pin's own _n plus the owner's call count:
# the loop only prints 0/1/2 and reaches 3 if every element slot kept the class.
#
# Expected UART output:
#   0
#   1
#   2
#   3
#   done
import pinlib
from pymcu.hal.console import print
from pymcu.hal.uart import UART

uart = UART(9600)

o = pinlib.Owner()
pins = [o.get_pin(i) for i in range(3)]
for p in pins:
    p.switch_to_output(value=False)

print(o.count)
print("done")

while True:
    pass
