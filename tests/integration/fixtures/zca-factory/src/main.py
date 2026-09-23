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
        raw: uint16 = pot.read_u16()
        led.duty_u16(raw)       # read_u16() and duty_u16() share the 0..65535 scale
        d: uint8 = uint8(raw >> 8)
        uart.write(d)           # echo raw >> 8, the 8-bit duty (sync marker for tests)
