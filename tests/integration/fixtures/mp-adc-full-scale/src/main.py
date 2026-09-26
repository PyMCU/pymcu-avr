# ADC.read_u16() scales the 10-bit reading the way the rp2 port scales its 12-bit one,
# raw << 6 | raw >> 4, so full scale is 65535. It was raw * 64, which stops at 65472.
# The test drives A0 to 5 V, A1 to 2.5 V and A2 to 0 V.
from machine import ADC

a0 = ADC(0)
a1 = ADC(1)
a2 = ADC(2)
print(a0.read_u16(), a1.read_u16(), a2.read_u16())
print("END")
