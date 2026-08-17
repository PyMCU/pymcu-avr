from pymcu.types import uint8, int8, int16, uint16, int32, uint32
from pymcu.hal.uart import UART
from pymcu.chips.atmega328p import GPIOR0


def flag(b: uint8) -> uint8:
    return b + 48


def main():
    uart = UART(9600)
    uart.println("WIDE")

    seed: uint8 = GPIOR0.value

    a16: int16 = 256 + seed
    b16: int16 = 3 + seed
    uart.write('A')
    uart.write(':')
    uart.write(flag(a16 < b16))
    uart.write(flag(a16 <= b16))
    uart.write(flag(a16 > b16))
    uart.write(flag(a16 >= b16))
    uart.write(flag(a16 == b16))
    uart.write(flag(a16 != b16))
    uart.write('\n')

    uart.write('B')
    uart.write(':')
    uart.write(flag(b16 < a16))
    uart.write(flag(b16 <= a16))
    uart.write(flag(b16 > a16))
    uart.write(flag(b16 >= a16))
    uart.write(flag(b16 == a16))
    uart.write(flag(b16 != a16))
    uart.write('\n')

    z16: int16 = 0 + seed
    uart.write('C')
    uart.write(':')
    uart.write(flag(a16 == z16))
    uart.write(flag(a16 != z16))
    uart.write('\n')

    au16: uint16 = 0x8000 + seed
    bu16: uint16 = 5 + seed
    uart.write('D')
    uart.write(':')
    uart.write(flag(au16 < bu16))
    uart.write(flag(au16 <= bu16))
    uart.write(flag(au16 > bu16))
    uart.write(flag(au16 >= bu16))
    uart.write(flag(au16 == bu16))
    uart.write(flag(au16 != bu16))
    uart.write('\n')

    zu16: uint16 = 0 + seed
    uart.write('E')
    uart.write(':')
    uart.write(flag(au16 == zu16))
    uart.write(flag(au16 != zu16))
    uart.write('\n')

    a32: int32 = 65536 + seed
    b32: int32 = 3 + seed
    uart.write('F')
    uart.write(':')
    uart.write(flag(a32 < b32))
    uart.write(flag(a32 <= b32))
    uart.write(flag(a32 > b32))
    uart.write(flag(a32 >= b32))
    uart.write(flag(a32 == b32))
    uart.write(flag(a32 != b32))
    uart.write('\n')

    uart.write('G')
    uart.write(':')
    uart.write(flag(b32 < a32))
    uart.write(flag(b32 <= a32))
    uart.write(flag(b32 > a32))
    uart.write(flag(b32 >= a32))
    uart.write(flag(b32 == a32))
    uart.write(flag(b32 != a32))
    uart.write('\n')

    z32: int32 = 0 + seed
    uart.write('H')
    uart.write(':')
    uart.write(flag(a32 == z32))
    uart.write(flag(a32 != z32))
    uart.write('\n')

    au32: uint32 = 0x80000000 + seed
    bu32: uint32 = 5 + seed
    uart.write('I')
    uart.write(':')
    uart.write(flag(au32 < bu32))
    uart.write(flag(au32 <= bu32))
    uart.write(flag(au32 > bu32))
    uart.write(flag(au32 >= bu32))
    uart.write(flag(au32 == bu32))
    uart.write(flag(au32 != bu32))
    uart.write('\n')

    zu32: uint32 = 0 + seed
    uart.write('J')
    uart.write(':')
    uart.write(flag(au32 == zu32))
    uart.write(flag(au32 != zu32))
    uart.write('\n')

    a8: int8 = 100 + seed
    b8: int8 = 3 + seed
    uart.write('K')
    uart.write(':')
    uart.write(flag(a8 < b8))
    uart.write(flag(a8 <= b8))
    uart.write(flag(a8 > b8))
    uart.write(flag(a8 >= b8))
    uart.write(flag(a8 == b8))
    uart.write(flag(a8 != b8))
    uart.write('\n')

    n16: int16 = 0 - 300 - seed
    p16: int16 = 5 + seed
    uart.write('L')
    uart.write(':')
    uart.write(flag(n16 < p16))
    uart.write(flag(n16 <= p16))
    uart.write(flag(n16 > p16))
    uart.write(flag(n16 >= p16))
    uart.write(flag(n16 == p16))
    uart.write(flag(n16 != p16))
    uart.write('\n')

    uart.println("END")

    while True:
        pass
