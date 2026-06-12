# RFC 0001 Model B (SRAM slot) -- a multi-field ZCA. A single field fits in a register
# (Model A / register handle), but >= 2 fields don't, so the instance is "boxed": its
# fields live in a fixed SRAM slot and its @outline method takes a `self` pointer, reading
# each field via BytearrayLoad at a byte offset. Two instances => two 2-byte slots, ONE
# shared Sensor_read body that walks the pointer it is handed.
#
#   a = Sensor(3, 4)  -> slot {pin:3, gain:4}; a.read() = 3*4 = 12
#   b = Sensor(5, 7)  -> slot {pin:5, gain:7}; b.read() = 5*7 = 35
#
# UART output: "SL\n" banner, then 12, then 35.
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
    uart.println("SL")

    a = Sensor(3, 4)
    b = Sensor(5, 7)

    uart.write(a.read())   # 12
    uart.write(b.read())   # 35

    while True:
        pass
