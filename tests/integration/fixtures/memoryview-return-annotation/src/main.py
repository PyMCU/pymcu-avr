# memoryview-return-annotation: Adafruit pca9685 annotates
#   def _get_buffer(...) -> memoryview
# memoryview() already lowers as a compile-time view of a fixed buffer.
# The return annotation was refused as an unknown type.
#
# WHAT DISCRIMINATES: 3 (first byte of buf through the annotated return).
from pymcu.types import uint8
from pymcu.time import delay_ms

buf = bytearray([3, 4, 5])


def view_of(b: bytearray) -> memoryview:
    return memoryview(b)


def main():
    while True:
        v = view_of(buf)
        print(v[0])
        print("END")
        delay_ms(1200)
