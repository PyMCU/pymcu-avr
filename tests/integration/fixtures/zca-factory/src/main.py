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
        raw: uint16 = pot.read()
        led.duty(raw)           # MicroPython's legacy duty() is 0..1023, the ADC's own scale
        d: uint8 = uint8(raw >> 2)
        uart.write(d)           # echo raw >> 2 (sync marker for tests)
