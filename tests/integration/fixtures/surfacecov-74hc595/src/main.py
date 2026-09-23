# Surface coverage: adafruit_74hc595 -- ShiftRegister74HC595 and DigitalInOut.
# Every public member is exercised on BOTH construction paths (hardware SPI and
# bit-banged clock/data) and the result printed; CPython runs the same file
# under the surfacecov oracle fakes and the two serial streams must match.
# fixture.json attaches SPI; read bytes are not consumed (the chip is
# write-only). The `assert` bound in get_pin is a documented gap: PyMCU drops
# runtime asserts, so only in-range pins are probed here.

import board
import busio
import digitalio
import adafruit_74hc595
from adafruit_74hc595 import ShiftRegister74HC595, DigitalInOut

print("= module")
print(adafruit_74hc595.__version__)
print(adafruit_74hc595.__repo__)

spi = busio.SPI(board.SCK, MOSI=board.MOSI, MISO=board.MISO)
latch = digitalio.DigitalInOut(board.D10)

print("= construct spi")
sr = ShiftRegister74HC595(spi, latch)
print(sr.number_of_shift_registers)
latch2 = digitalio.DigitalInOut(board.D9)
sr2 = ShiftRegister74HC595(spi, latch2, number_of_shift_registers=2, baudrate=500000)
print(sr2.number_of_shift_registers)

print("= gpio")
g1 = bytearray(1)
g1[0] = 0xA5
sr.gpio = g1
print(sr.gpio[0])
g2 = bytearray(2)
g2[0] = 0x3C
g2[1] = 0x81
sr2.gpio = g2
print(sr2.gpio[0])
print(sr2.gpio[1])

print("= get_pin")
d = sr.get_pin(3)
print(isinstance(d, DigitalInOut))
d9 = sr2.get_pin(9)
print(isinstance(d9, DigitalInOut))
dd = DigitalInOut(5, sr)
print(isinstance(dd, DigitalInOut))

print("= switch_to_output / value")
d.switch_to_output(value=True)
print(d.value)
d.value = False
print(d.value)
print(sr.gpio[0])

print("= direction")
print(d.direction)
d.direction = digitalio.Direction.OUTPUT
print(d.direction)
try:
    d.direction = digitalio.Direction.INPUT
except RuntimeError as e:
    print("R:", e)

print("= pull")
print(d.pull is None)
d.pull = None
print(d.pull is None)
try:
    d.pull = digitalio.Pull.UP
except RuntimeError as e:
    print("R:", e)

print("= switch_to_input")
try:
    d.switch_to_input()
except RuntimeError as e:
    print("R:", e)

print("= construct errors")
try:
    ShiftRegister74HC595(spi, None)
except ValueError as e:
    print("V:", e)
try:
    ShiftRegister74HC595(None, latch)
except ValueError as e:
    print("V:", e)

print("= construct bitbang")
blatch = digitalio.DigitalInOut(board.D8)
bclock = digitalio.DigitalInOut(board.D7)
bdata = digitalio.DigitalInOut(board.D6)
srb = ShiftRegister74HC595(None, blatch, clock=bclock, data=bdata,
                           number_of_shift_registers=2)
print(srb.number_of_shift_registers)

print("= bitbang gpio / pin")
gb = bytearray(2)
gb[0] = 0xA5
gb[1] = 0x3C
srb.gpio = gb
print(srb.gpio[0])
print(srb.gpio[1])
db = srb.get_pin(9)
print(isinstance(db, DigitalInOut))
db.switch_to_output(value=True)
print(db.value)
db.value = False
print(db.value)
print(srb.gpio[1])
print(db.direction)
print(db.pull is None)
db.pull = None
try:
    db.switch_to_input()
except RuntimeError as e:
    print("R:", e)

print("=DONE=")
