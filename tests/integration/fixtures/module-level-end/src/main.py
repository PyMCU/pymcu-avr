# A program whose module level simply ends: drive the LED high and fall off the
# end of main. The entry function is reached by RJMP with an empty hardware
# stack, so its trailing return used to RET into unimplemented SRAM and either
# reboot or run wild. Now it parks the CPU on __pymcu_halt (cli + spin), the
# avr-libc _exit idiom: PB5 stays high and nothing runs again.
from machine import Pin

led = Pin(13, Pin.OUT)
led.high()
