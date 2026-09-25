"""CPython oracle for the verbatim-adafruit-dht fixture.

The emulator feeds the firmware a fixed DHT22 frame (humidity 550 tenths,
temperature 235 tenths) through the Dht22Simulator; this script computes what
the fixture's src/main.py prints for that frame -- the same tenths-to-float
decode adafruit_dht does (raw / 10.0) -- then runs the verbatim simpletest's
own arithmetic and f-string under stock CPython, so the expected line is
byte-for-byte what upstream produces.
"""

# The frame the C# Dht22Simulator injects (must match the test's Respond call).
HUMIDITY_TENTHS = 550      # 55.0 %
TEMPERATURE_TENTHS = 235   # 23.5 C

temperature_c = TEMPERATURE_TENTHS / 10
humidity = HUMIDITY_TENTHS / 10
temperature_f = temperature_c * (9 / 5) + 32
print(f"Temp: {temperature_f:.1f} F / {temperature_c:.1f} C    Humidity: {humidity}% ")
