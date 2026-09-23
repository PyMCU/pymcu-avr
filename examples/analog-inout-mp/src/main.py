# Arduino "AnalogInOutSerial" (03.Analog), ported to PyMCU on the MicroPython
# compatibility layer (machine).
#
# Reads a potentiometer on A0, drives the PWM duty on D6 (OC0A) with it directly
# (read_u16() and duty_u16() share the upstream 0..65535 scale) and echoes the
# 8-bit duty over UART. Exercises machine.ADC, machine.PWM (both Pin
# overloads -- Pin(14) int for A0 and Pin("PD6") string) and machine.UART.
from machine import Pin, ADC, PWM, UART
from pymcu.types import uint8, uint16


def main():
    uart = UART(0, 9600)
    pot = ADC(Pin(14))        # Pin(14) == A0 == PC0  (int->name overload)
    led = PWM(Pin("PD6"))     # OC0A PWM output        (str overload)
    led.init()

    while True:
        sensor: uint16 = pot.read_u16()  # 0..65535
        led.duty_u16(sensor)             # pwm.duty_u16(adc.read_u16()), the rp2 idiom
        out: uint8 = uint8(sensor >> 8)  # the 8-bit duty the timer runs at
        uart.write(out)                  # echo it (sync marker for tests)
