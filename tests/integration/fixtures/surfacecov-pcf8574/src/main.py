# Surface coverage: adafruit_pcf8574 -- PCF8574 and DigitalInOut.
# Every public member is exercised and the result printed; CPython runs the
# same file under the surfacecov oracle fakes and the two serial streams must
# match line for line. fixture.json ACKs 0x20/0x21 and readscript.txt feeds
# the GPIO read bytes in order.

import board
import busio
import digitalio
import adafruit_pcf8574
from adafruit_pcf8574 import PCF8574, DigitalInOut, PCF8574_I2CADDR_DEFAULT

print("= module")
print(adafruit_pcf8574.__version__)
print(adafruit_pcf8574.__repo__)
print(PCF8574_I2CADDR_DEFAULT)

i2c = busio.I2C(board.SCL, board.SDA)

print("= construct")
pcf = PCF8574(i2c)
print(pcf.i2c_device.device_address)
pcf2 = PCF8574(i2c, 0x21)
print(pcf2.i2c_device.device_address)

print("= write_gpio / read_gpio")
pcf.write_gpio(0xA5)
print(pcf.read_gpio())
pcf.write_gpio(0x00)

print("= write_pin / read_pin")
pcf.write_pin(3, True)
pcf.write_pin(3, False)
pcf.write_pin(7, True)
print(pcf.read_pin(1))
print(pcf.read_pin(7))

print("= get_pin")
d = pcf.get_pin(2)
print(isinstance(d, DigitalInOut))
d2 = pcf.get_pin(0)
print(isinstance(d2, DigitalInOut))
d3 = DigitalInOut(5, pcf)
print(isinstance(d3, DigitalInOut))

print("= switch_to_output / value")
d.switch_to_output(value=True)
print(d.direction)
print(d.value)
d.value = False
print(d.value)

print("= switch_to_input / pull")
d.switch_to_input(pull=digitalio.Pull.UP)
print(d.direction)
print(d.pull)
print(d.value)

print("= direction / pull errors")
rt = pcf.read_gpio()
try:
    d.direction = (rt & 0) + 3
except ValueError as e:
    print("V:", e)
try:
    d.pull = digitalio.Pull.DOWN if rt else digitalio.Pull.UP
except NotImplementedError as e:
    print("N:", e)
print(d.pull)

print("=DONE=")
