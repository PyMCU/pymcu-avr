# struct-calcsize-param: Adafruit StructArray(0x06, "<HH", 16) shape.
#
# A class-body descriptor constructor calls _fit(struct.calcsize(struct_format))
# with struct_format: str. The format used to arrive as an interned id, so
# calcsize refused a string the compiler was holding and _BUFFER stayed 1 byte.
#
# WHAT DISCRIMINATES: 7 written at _BUFFER[4]. calcsize("<HH") is 4, so _fit
# grows the 1-byte scratch buffer to 5. A compile that still refused the
# format would not build; one that left the buffer at 1 would IndexError.
from pymcu.time import delay_ms
from sensor import Dev
from pack import _BUFFER

d = Dev()


def main():
    while True:
        _BUFFER[4] = 7
        print(_BUFFER[4])
        print("END")
        delay_ms(1200)
