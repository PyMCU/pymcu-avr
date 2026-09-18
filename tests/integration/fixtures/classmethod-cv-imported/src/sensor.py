from pymcu.types import uint8


class CV:
    @classmethod
    def add_values(cls, value_tuples):
        cls.string = {}
        for value_tuple in value_tuples:
            name, value, string = value_tuple
            setattr(cls, name, value)
            cls.string[value] = string


class Mode(CV):
    pass


Mode.add_values((
    ("NOHEAT_HIGHPRECISION", 0xFD, "hi"),
    ("NOHEAT_MEDPRECISION", 0xF6, "med"),
    ("NOHEAT_LOWPRECISION", 0xE0, "lo"),
    ("HIGHHEAT_1S", 0x39, "h1"),
    ("HIGHHEAT_100MS", 0x32, "h0"),
    ("MEDHEAT_1S", 0x2F, "m1"),
    ("MEDHEAT_100MS", 0x24, "m0"),
    ("LOWHEAT_1S", 0x1E, "l1"),
    ("LOWHEAT_100MS", 0x15, "l0"),
))


class SHT:
    def __init__(self):
        self._mode = Mode.NOHEAT_HIGHPRECISION
