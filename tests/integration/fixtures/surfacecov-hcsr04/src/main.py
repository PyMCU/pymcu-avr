# test201_surface_hcsr04.py -- whole-surface probe for adafruit_hcsr04.HCSR04.
# Members exercised (surface.tsv): __init__ (trigger_pin, echo_pin, timeout kw),
# distance (property get), deinit(), __enter__/__exit__ via a with block.
#
# Pin plan is dictated by the HAL, not by taste: pulse_capture_attach() can put
# pulse_isr on only ONE vector per program, and on this part PD2/PD3 capture
# through INT0/INT1 (external interrupts) while every other pin shares a PCINT
# group vector.  Both sonars therefore read the SAME echo pin (D2 -> INT0) and
# differ only in trigger pin (D4, D7 -- plain outputs).  fixture.json answers
# per trigger pin in entry order: D4 replies 1000us, 580us then 70000us (which
# trips the pulselen >= 65535 RuntimeError); D7 replies 430us once; the with-block
# read gets no reply at all and trips the monotonic-timeout RuntimeError.
import board

from adafruit_hcsr04 import HCSR04

s1 = HCSR04(trigger_pin=board.D4, echo_pin=board.D2)
print(s1.distance)
print(s1.distance)

try:
    print(s1.distance)
except RuntimeError as e:
    print(e)

s2 = HCSR04(board.D7, board.D2, timeout=0.2)
try:
    print(s2.distance)
except RuntimeError as e:
    print(e)

with s2 as s:
    try:
        print(s.distance)
    except RuntimeError as e:
        print(e)

s1.deinit()

print("=DONE=")
