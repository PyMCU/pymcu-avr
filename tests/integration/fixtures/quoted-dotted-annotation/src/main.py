# quoted-dotted-annotation: obj: "Pack.Dev" is obj: Pack.Dev.
#
# adafruit_si7021 writes
#   obj: "adafruit_si7021.SI7021"
# as a quoted dotted class. The C# parser only unquoted a bare identifier.
#
# WHAT DISCRIMINATES: prints 17. A compile that refused the quotes would
# not build; a compile that dropped obj.a would not print 17.
from pymcu.types import uint8
from pymcu.time import delay_ms


class Pack:
    class Dev:
        def __init__(self):
            self.a: uint8 = 17


def read(obj: "Pack.Dev") -> uint8:
    return obj.a


def main():
    while True:
        print(read(Pack.Dev()))
        print("END")
        delay_ms(1200)
