# Arduino "AnalogInOutSerial" (03.Analog), native PyMCU HAL.
# Read A0, map 0-1023 -> 0-255 PWM duty on D6, echo duty over UART.
from pymcu.types import uint8, uint16
from pymcu.hal.uart import UART
from pymcu.hal.adc import AnalogPin
from pymcu.hal.pwm import PWM


def main():
    uart = UART(9600)
    pot = AnalogPin("PC0")
    led = PWM("PD6", 0)
    led.start()

    while True:
        sensor: uint16 = pot.read()
        out: uint8 = uint8(sensor >> 2)
        led.set_duty(out)
        uart.write(out)
