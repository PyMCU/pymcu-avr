# Adafruit dps310 / pca9685 shape: [None] * N is a fixed SRAM array.
# fill() is coeffs = [None] * 18 then a real range(18) store (dps310).
# __init__ writes self.ch = [None] * len(self) with __len__ after it (pca9685).
from pymcu.types import uint8


class Dev:
    def __init__(self) -> None:
        self.ch = [None] * len(self)
        self.ch[2] = 7

    def __len__(self) -> int:
        return 4

    def fill(self) -> uint8:
        coeffs = [None] * 18
        for offset in range(18):
            coeffs[offset] = offset
        return coeffs[6]
