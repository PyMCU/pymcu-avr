"""CPython oracle for the unmodified-adafruit_dht fixture.

The emulator feeds the firmware a fixed DHT22 frame (humidity 550 tenths,
temperature 235 tenths) through the Dht22Simulator; this script computes what
the fixture's src/main.py prints for that frame -- the same tenths-to-float
decode adafruit_dht does (raw / 10.0), then the verbatim simpletest line.

Floats render in PyMCU's print() format: two rounded decimals with the
hundredths digit dropped when it is zero (74.30000000000001 -> "74.3",
55.0 -> "55.0"), the contract uart_write_float documents. The values come out
of CPython's own float arithmetic, so the line validates both the tag dispatch
and the math the narrowed member feeds.
"""

# The frame the C# Dht22Simulator injects (must match the test's Respond call).
HUMIDITY_TENTHS = 550      # 55.0 %
TEMPERATURE_TENTHS = 235   # 23.5 C


def fmt(value: float) -> str:
    s = f"{value:.2f}"
    return s[:-1] if s.endswith("0") else s


t = TEMPERATURE_TENTHS / 10
h = HUMIDITY_TENTHS / 10
temperature_f = t * (9 / 5) + 32
print("Temp:", fmt(temperature_f), "F /", fmt(t), "C    Humidity:", fmt(h), "%")
