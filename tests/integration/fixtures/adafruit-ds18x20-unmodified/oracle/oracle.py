"""CPython oracle for the unmodified-adafruit_ds18x20 fixture.

The OneWireSlaveSimulator answers as a DS18B20 whose scratchpad holds the raw reading below.
The driver decodes it as raw / 16 (adafruit_ds18x20._read_temp, family code 0x28) and the
fixture's src/main.py, the library's simpletest verbatim, prints it with :0.3f. This script
prints the same line under stock CPython.
"""

# The raw 12-bit reading the C# OneWireSlaveSimulator puts in the scratchpad (must match the test).
RAW = 0x0178        # 376 / 16 = 23.5 C

t = RAW / 16
print(f"Temperature: {t:0.3f}C")
