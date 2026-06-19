# Massive stress: 4-channel ADC DSP exercising every AVR codegen optimization
# at once -- byte-pack (4 sensor reads), Z-CSE (sliding windows), 16-bit temp
# allocation (heavy uint16 math), divmod fusion (//, decimal print) and the
# divmod() builtin.
from pymcu.types import uint8, uint16
from pymcu.hal.uart import UART
from pymcu.hal.adc import AnalogPin


def main():
    uart = UART(9600)
    uart.println("DSP")
    ch0 = AnalogPin("PC0")
    ch1 = AnalogPin("PC1")
    ch2 = AnalogPin("PC2")
    ch3 = AnalogPin("PC3")

    buf0: uint16[8] = [0, 0, 0, 0, 0, 0, 0, 0]
    buf1: uint16[8] = [0, 0, 0, 0, 0, 0, 0, 0]
    buf2: uint16[8] = [0, 0, 0, 0, 0, 0, 0, 0]
    buf3: uint16[8] = [0, 0, 0, 0, 0, 0, 0, 0]
    idx: uint8 = 0
    tot0: uint16 = 0
    tot1: uint16 = 0
    tot2: uint16 = 0
    tot3: uint16 = 0
    vmin: uint16 = 65535
    vmax: uint16 = 0

    while True:
        s0: uint16 = ch0.read()
        s1: uint16 = ch1.read()
        s2: uint16 = ch2.read()
        s3: uint16 = ch3.read()

        tot0 = tot0 - buf0[idx]
        buf0[idx] = s0
        tot0 = tot0 + buf0[idx]
        tot1 = tot1 - buf1[idx]
        buf1[idx] = s1
        tot1 = tot1 + buf1[idx]
        tot2 = tot2 - buf2[idx]
        buf2[idx] = s2
        tot2 = tot2 + buf2[idx]
        tot3 = tot3 - buf3[idx]
        buf3[idx] = s3
        tot3 = tot3 + buf3[idx]

        idx = idx + 1
        if idx >= 8:
            idx = 0

        a0: uint16 = tot0 // 8
        a1: uint16 = tot1 // 8
        a2: uint16 = tot2 // 8
        a3: uint16 = tot3 // 8

        if a0 < vmin:
            vmin = a0
        if a0 > vmax:
            vmax = a0

        q: uint16 = 0
        r: uint16 = 0
        q, r = divmod(a0, 10)

        print(a0)
        print(a1)
        print(a2)
        print(a3)
        print(q)
        print(r)
        print(vmin)
        print(vmax)
