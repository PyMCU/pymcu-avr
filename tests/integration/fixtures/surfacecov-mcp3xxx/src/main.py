# Surface coverage: adafruit_mcp3xxx -- MCP3008 and AnalogIn over SPI.
# Every public member is read or written and the result printed; CPython runs
# the same file under the surfacecov oracle fakes and the two serial streams
# must match line for line. The SPI slave answers each write_readinto from
# readscript.txt: 3 bytes per read, the 10-bit result in in_buf[1]&3 / in_buf[2].
# The abstract MCP3xxx base is exercised through MCP3008, the way the library
# intends (it is never constructed directly).

import board
import busio
import digitalio
from adafruit_mcp3xxx import mcp3xxx
from adafruit_mcp3xxx.mcp3008 import MCP3008, P0, P1, P2, P3, P4, P5, P6, P7
from adafruit_mcp3xxx.analog_in import AnalogIn

print("= mcp3xxx module")
print(mcp3xxx.__version__)
print(mcp3xxx.__repo__)

print("= pin aliases")
print(P0)
print(P1)
print(P2)
print(P3)
print(P4)
print(P5)
print(P6)
print(P7)

spi = busio.SPI(board.SCK, MOSI=board.MOSI, MISO=board.MISO)
cs = digitalio.DigitalInOut(board.D10)

print("= MCP3008() defaults")
mcp = MCP3008(spi, cs)
print(mcp.reference_voltage)
print(MCP3008.BITS)
print(len(MCP3008.DIFF_PINS))
print(MCP3008.DIFF_PINS[(0, 1)])
print(MCP3008.DIFF_PINS[(7, 6)])

print("= MCP3008() keywords")
mcp2 = MCP3008(spi, cs, ref_voltage=5.0, baudrate=500000)
print(mcp2.reference_voltage)

print("= read() single-ended")
print(mcp.read(P3))              # script: 677
print(mcp.read(P0))              # script: 0
print(mcp.read(P7))              # script: 1023

print("= read() differential")
print(mcp.read(P0, is_differential=True))   # script: 512

print("= AnalogIn() single-ended")
ain = AnalogIn(mcp, P5)
print(ain.is_differential)
print(ain.value)                 # read -> 300 -> stretched 19218
print(ain.voltage)               # read -> 700 -> 44843 * 3.3 / 65535

print("= AnalogIn() differential")
diff = AnalogIn(mcp, P0, P1)
print(diff.is_differential)
print(diff.value)                # read -> 100 -> stretched 6406
print(diff.voltage)              # read -> 1023 -> 3.3

print("= AnalogIn() errors")
try:
    AnalogIn("not an mcp", P0)
except ValueError as e:
    print(e)
try:
    pass
except ValueError as e:
    print(e)

print("=DONE=")
