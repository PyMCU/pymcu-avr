# RFC 0001 Model B (Class[N]) -- an array of boxed ZCA instances. The "multiples DHT"
# case: N sensors laid out contiguously in SRAM, each its own slot, all driven by ONE shared
# method through a runtime index. sensors[i] is the slot at base + i*stride; sensors[i].read()
# passes that element address as the self pointer.
#
#   sensors[0] = Sensor(3, 4)   # slot 0: pin=3, gain=4 -> read() = 12
#   sensors[1] = Sensor(5, 7)   # slot 1: pin=5, gain=7 -> read() = 35
#   sensors[2] = Sensor(2, 9)   # slot 2: pin=2, gain=9 -> read() = 18
#
# A runtime-indexed loop calls the single shared Sensor_read for each element.
# UART output: "AR\n" banner, then 12, 35, 18.
from pymcu.types import uint8
from pymcu.hal.uart import UART


class Sensor:
    def __init__(self, pin: uint8, gain: uint8):
        self.pin = pin
        self.gain = gain

    def read(self) -> uint8:
        return self.pin * self.gain


def main():
    uart = UART(9600)
    uart.println("AR")

    sensors: Sensor[3]
    sensors[0] = Sensor(3, 4)
    sensors[1] = Sensor(5, 7)
    sensors[2] = Sensor(2, 9)

    i: uint8 = 0
    while i < 3:
        uart.write(sensors[i].read())   # runtime index -> shared Sensor_read
        i = i + 1

    while True:
        pass
