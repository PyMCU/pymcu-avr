# classmethod-cv: Mode.add_values populates class attributes at compile time.
#
# adafruit_sht4x / adafruit_tmp117 write
#   class CV:
#       @classmethod
#       def add_values(cls, value_tuples):
#           cls.string = {}
#           ...
#           setattr(cls, name, value)
#           cls.string[value] = string
#   Mode.add_values((("NOHEAT_HIGHPRECISION", 0xFD, "...", 0.01), ...))
#
# WHAT DISCRIMINATES: prints 253, 1, 0, 1. A compile that still refused
# @classmethod would not build; setattr that did not bind Mode.NOHEAT
# would print the zeros they started as.
from pymcu.types import uint8
from pymcu.time import delay_ms


class CV:
    @classmethod
    def add_values(cls, value_tuples):
        cls.string = {}
        cls.delay = {}
        for value_tuple in value_tuples:
            name, value, string, delay = value_tuple
            setattr(cls, name, value)
            cls.string[value] = string
            cls.delay[value] = delay

    @classmethod
    def is_valid(cls, value: uint8) -> uint8:
        return 1 if value in cls.string else 0


class Mode(CV):
    pass


Mode.add_values((
    ("NOHEAT_HIGHPRECISION", 0xFD, "No heat high precision", 0.01),
    ("NOHEAT_MEDPRECISION", 0xF6, "No heat med precision", 0.004),
    ("NOHEAT_LOWPRECISION", 0xE0, "No heat low precision", 0.001),
    ("HIGHHEAT_HIGHPRECISION", 0x39, "High heat high precision", 1.1),
    ("HIGHHEAT_MEDPRECISION", 0x32, "High heat med precision", 1.1),
    ("HIGHHEAT_LOWPRECISION", 0x24, "High heat low precision", 1.1),
    ("MEDHEAT_HIGHPRECISION", 0x2F, "Med heat high precision", 1.1),
    ("MEDHEAT_MEDPRECISION", 0x24, "Med heat med precision", 1.1),
    ("MEDHEAT_LOWPRECISION", 0x16, "Med heat low precision", 1.1),
))


def main():
    while True:
        print(Mode.NOHEAT_HIGHPRECISION)
        print(Mode.is_valid(0xFD))
        print(Mode.is_valid(0))
        print(uint8(Mode.delay[0xFD] * 100))
        print("END")
        delay_ms(1200)
