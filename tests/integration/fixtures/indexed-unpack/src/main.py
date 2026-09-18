# indexed-unpack: word[i], crc[i] = struct.unpack(...) stores each field.
#
# adafruit_sht31d writes
#   word[i * 2], crc[i * 2], word[(i * 2) + 1], crc[(i * 2) + 1] = struct.unpack(
#       ">HBHB", data[i * 6 : (i * 6) + 6]
#   )
#
# WHAT DISCRIMINATES: prints 18, 171, 86, 205. A compile that refused the
# comma after word[i*2] would not build; stores that missed the arrays
# would print the zeros they started as.
from pymcu.types import uint8, uint16
from pymcu.time import delay_ms
import struct


def main():
    data: uint8[6] = [0x12, 0x34, 0xAB, 0x56, 0x78, 0xCD]
    word: uint16[4] = [0, 0, 0, 0]
    crc: uint8[4] = [0, 0, 0, 0]
    i: uint8 = 0
    word[i * 2], crc[i * 2], word[(i * 2) + 1], crc[(i * 2) + 1] = struct.unpack(
        ">HBHB", data[i * 6 : (i * 6) + 6]
    )
    while True:
        print(word[0] >> 8)
        print(crc[0])
        print(word[1] >> 8)
        print(crc[1])
        print("END")
        delay_ms(1200)
