# ATtiny84: classic LED blink on PA0 (avr25 core).
# Demonstrates PyMCU's ATtiny support: per-chip RAMEND and RJMP-only codegen.
from pymcu.hal.gpio import Pin
from pymcu.time import delay_ms


def main():
    led = Pin("PA0", Pin.OUT)
    while True:
        led.high()
        delay_ms(100)
        led.low()
        delay_ms(100)
