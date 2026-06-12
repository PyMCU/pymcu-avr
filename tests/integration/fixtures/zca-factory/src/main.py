# An @inline factory may return a ZCA instance; the result's methods must still
# inline. `a = make_adc()` used to mangle a.read() to an undefined symbol.
from machine import Pin, ADC, PWM, UART
from pymcu.types import inline, uint8


@inline
def make_adc(ch: uint8) -> ADC:
    return ADC(Pin(ch))


def main():
    uart = UART(0, 9600)
    pot = make_adc(14)          # A0 via factory
    led = PWM(Pin("PD6"))
    led.init()
    while True:
        d: uint8 = uint8(pot.read() >> 2)
        led.duty(d)
        uart.write(d)           # echo duty (sync marker for tests)
