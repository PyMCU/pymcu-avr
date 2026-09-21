# A program whose module level can never end: the appended Return is unreachable
# after `while True:` and the CFG pass deletes it before codegen, so the image
# must not contain __pymcu_halt at all -- a never-ending program pays nothing
# for the park-the-CPU fix.
from machine import Pin

led = Pin(13, Pin.OUT)
led.high()

while True:
    pass
