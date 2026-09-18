# property-tuple-index: self.measurements[0] on a tuple-returning @property.
#
# adafruit_sht4x writes
#   @property
#   def temperature(self):
#       return self.measurements[0]
#   @property
#   def measurements(self) -> Tuple[float, float]:
#       return (temperature, humidity)
#
# WHAT DISCRIMINATES: prints 10, 20. A compile that still treated the
# property as a scalar would refuse "'measurements' returns 2 values".
from pymcu.types import uint8
from pymcu.time import delay_ms


class SHT:
    def __init__(self):
        pass

    @property
    def measurements(self) -> (uint8, uint8):
        return (10, 20)

    @property
    def temperature(self) -> uint8:
        return self.measurements[0]

    @property
    def humidity(self) -> uint8:
        return self.measurements[1]


s = SHT()


def main():
    while True:
        print(s.temperature)
        print(s.humidity)
        print("END")
        delay_ms(1200)
