# Arduino "AnalogInOutSerial" (03.Analog), ported to PyMCU on the MicroPython
# compatibility layer (machine).
#
# Reads a potentiometer on A0, drives the PWM duty on D6 (OC0A) with it directly
# (MicroPython's legacy duty() is 0..1023, the ADC's own scale) and echoes the
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
        sensor: uint16 = pot.read()      # 0..1023
        led.duty(sensor)                 # pwm.duty(adc.read()), the MicroPython idiom
        out: uint8 = uint8(sensor >> 2)  # the 8-bit duty the timer runs at
        uart.write(out)                  # echo it (sync marker for tests)
