# descriptor-setter-any-value: Adafruit UnaryStruct.__set__ shape.
#
# d.bits = 7 rewrites to Field.__set__(d, 7) with value: Any. #367 accepted
# Any on an unread parameter and refused the first read as having no width,
# even though the written value is an int. That is what stopped
# adafruit_ina219 after _fit kept the shared buffer.
#
# WHAT DISCRIMINATES: 7. A compile that still treated Any as a missing width
# would not build.
from pymcu.types import uint8
from pymcu.time import delay_ms


class Field:
    def __init__(self) -> None:
        pass

    def __set__(self, obj, value: Any) -> None:
        obj.reg = value


class Dev:
    bits = Field()

    def __init__(self) -> None:
        self.reg: uint8 = 0


d = Dev()


def main():
    while True:
        d.bits = 7
        print(d.reg)
        print("END")
        delay_ms(1200)
