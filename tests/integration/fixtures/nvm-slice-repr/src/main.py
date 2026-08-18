# CircuitPython's canonical nvm pattern: a 4-byte slice write from a bytes
# literal (unrolled to per-byte __setitem__ EEPROM writes) and a slice read
# printed as the CPython bytearray repr. Also covers print(bytearray) for a
# local buffer, which used to stream the array VARIABLE through decimal_u8
# and print garbage.
#
# Expected UART output (9600 baud):
#   bytearray(b'\xcc\x10\xca\xfe')
#   bytearray(b'\x01A\n')
#   D
import microcontroller
from pymcu.types import uint8


def main():
    microcontroller.nvm[0:4] = b'\xcc\x10\xca\xfe'
    print(microcontroller.nvm[0:4])
    buf: bytearray = bytearray(3)
    buf[0] = 1
    buf[1] = 65
    buf[2] = 10
    print(buf)
    print("D", end="")
    while True:
        pass
