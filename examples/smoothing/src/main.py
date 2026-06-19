# Arduino "Smoothing" example, ported to PyMCU.
# Keeps a running average of the last 10 analog readings from A0 (PC0) and
# prints it over UART -- the classic sliding-window smoothing filter.
#
# Original: File > Examples > 03.Analog > Smoothing
# Wiring: potentiometer / sensor wiper -> A0, UART TX at 9600 baud.
from pymcu.types import uint8, uint16
from pymcu.hal.uart import UART
from pymcu.hal.adc import AnalogPin
from pymcu.time import delay_ms


def main():
    uart = UART(9600)
    sensor = AnalogPin("PC0")
    readings: uint16[10] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
    index: uint8 = 0
    total: uint16 = 0

    while True:
        # subtract the oldest reading, sample, add the newest
        total = total - readings[index]
        readings[index] = sensor.read()
        total = total + readings[index]

        index = index + 1
        if index >= 10:
            index = 0

        average: uint16 = total // 10
        print(average)
        delay_ms(1)
