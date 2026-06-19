# Arduino "AnalogInOutSerial" (03.Analog), ported to PyMCU on the MicroPython
# compatibility layer (machine).
#
# Reads a potentiometer on A0, maps 0-1023 to a 0-255 PWM duty on D6 (OC0A), and
# echoes the duty byte over UART. Exercises machine.ADC, machine.PWM (both Pin
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
        out: uint8 = uint8(sensor >> 2)  # map 0..1023 -> 0..255 (Arduino map)
        led.duty(out)                    # analogWrite(D6, out)
        uart.write(out)                  # echo duty byte (sync marker for tests)
