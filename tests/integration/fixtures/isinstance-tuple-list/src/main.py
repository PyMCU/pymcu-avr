# isinstance-tuple-list: PyMCU/PyMCU#423.
#
# adafruit_ht16k33's HT16K33.__init__ branches on
#   if isinstance(address, (tuple, list)):
# The ZCA-class fold (#424) does not answer: the candidates are the builtins
# tuple/list, not a class with a layout. The receiver's shape is still known
# at each constructor call site (Union[int, List, Tuple] from #442).
#
# WHAT DISCRIMINATES: 112 (0x70, the int arm) and 1 (the list arm's first
# element). A compile that still refused isinstance would not build.
#
# Expected UART output:
#   112
#   1
#   END
from pymcu.types import uint8


class Matrix:
    def __init__(self, address: Union[uint8, List[uint8], Tuple[uint8, ...]] = 0x70) -> None:
        if isinstance(address, (tuple, list)):
            self.address = address[0]
        else:
            self.address = address


def main() -> None:
    m1 = Matrix()
    m2 = Matrix([1, 2, 3])
    print(m1.address)
    print(m2.address)
    print("END")
    while True:
        pass
