# repeated-none-list: [None] * N is a fixed SRAM array.
#
# adafruit_dps310 writes coeffs = [None] * 18 then fills it in range(18).
# adafruit_pca9685 writes self._channels = [None] * len(self) in __init__,
# with __len__ defined after that.
#
# WHAT DISCRIMINATES: fill() prints 6 (coeffs[6] after a runtime-index store)
# and d.ch[2] prints 7. A compile that still visited the list as a value
# would not build. A compile that laid the repeat out as a scalar would
# treat ch[2] as bit 2 of a byte (0 or 1), not 7.
from pymcu.time import delay_ms
from sensor import Dev

d = Dev()


def main():
    while True:
        print(d.fill())
        print(d.ch[2])
        print("END")
        delay_ms(1200)
