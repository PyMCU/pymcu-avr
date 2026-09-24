"""CPython oracle for the adafruit-dht-optprint fixture.

Same injected frame as adafruit-dht-unmodified (humidity 550 tenths,
temperature 235 tenths). main.py prints `dhtDevice.temperature` and
`dhtDevice.humidity` unnarrowed -- the RFC 0009 decision-7 read -- so this
script computes what CPython prints for the decoded floats, in PyMCU's
print() float format (two rounded decimals, a trailing zero dropped).
"""

HUMIDITY_TENTHS = 550      # 55.0 %
TEMPERATURE_TENTHS = 235   # 23.5 C


def fmt(value: float) -> str:
    s = f"{value:.2f}"
    return s[:-1] if s.endswith("0") else s


t = TEMPERATURE_TENTHS / 10
h = HUMIDITY_TENTHS / 10
print(fmt(t), fmt(h))
print(f"t={fmt(t)} h={fmt(h)}")
