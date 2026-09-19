# member-listcomp-fill: obj.buf = [color for i in range(len(obj.buf))]
#
# adafruit GS2HMSBFormat.fill writes that assignment. A list
# comprehension in a value position is refused; assigned to a field
# that already is a fixed array, it fills that storage.
#
# WHAT DISCRIMINATES: prints 9 then 9.
from pymcu.time import delay_ms
from pymcu.types import uint8


class FB:
    def __init__(self):
        self.buf: uint8[2] = [1, 2]

    def fill(self, color: uint8):
        self.buf = [color for i in range(len(self.buf))]


fb = FB()


def main():
    while True:
        fb.fill(9)
        print(fb.buf[0])
        print(fb.buf[1])
        print("END")
        delay_ms(1200)
