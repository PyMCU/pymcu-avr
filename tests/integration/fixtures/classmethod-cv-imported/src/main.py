# classmethod-cv-imported: Mode.add_values in an imported module.
#
# adafruit_sht4x writes Mode.add_values((9 tuples...)) then
# SHT4x.__init__ does self._mode = Mode.NOHEAT_HIGHPRECISION.
# setattr used the mangled class key (adafruit_sht4x_Mode) and
# prefixed the module again, so the attribute was never Mode.NOHEAT.
#
# WHAT DISCRIMINATES: prints 253. A compile that still doubled the
# module prefix would refuse Mode.NOHEAT_HIGHPRECISION as missing.
from pymcu.time import delay_ms
from sensor import SHT

s = SHT()


def main():
    while True:
        print(s._mode)
        print("END")
        delay_ms(1200)
