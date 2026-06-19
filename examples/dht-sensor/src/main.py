# PyMCU DHT11 sensor example — Arduino Uno
#
# Demonstrates:
#   - Board abstractions:  pymcu.boards.arduino_uno
#   - Stdlib driver:       pymcu.drivers.dht11 (arch-neutral ZCA, same pattern as Pin/UART)
#   - Multi-file project:  main.py is the only local file; the driver lives in the stdlib
#
# sensor = DHT11(D2)   → D2 = "PD2" bound at compile time
# sensor.read()        → match __CHIP__.arch → match pin_name (const[str]) → shared protocol
#
# The driver stores only a const[str] pin name (a scalar field), so DHT11.read() is a
# zero-cost abstraction: the heavy bit-bang protocol lives in one shared _pd_read(bit)
# subroutine, not duplicated per instance.
#
# Wiring:
#   DHT11 DATA → Arduino D2 (4.7 kΩ pull-up to +5 V)
#   DHT11 VCC  → +5 V    DHT11 GND → GND
#
# UART output (9600 baud):
#   Boot:  "DHT11\n"
#   OK:    "H:XX T:XX\n"
#   Error: "ERR\n"
from pymcu.types import uint8, uint16
from pymcu.boards.arduino_uno import D2, LED_BUILTIN
from pymcu.hal.gpio import Pin
from pymcu.hal.uart import UART
from pymcu.time import delay_ms
from pymcu.drivers.dht11 import DHT11


def main():
    uart   = UART(9600)
    led    = Pin(LED_BUILTIN, Pin.OUT)
    sensor = DHT11(D2)   # ZCA: stores the const "PD2" name, no per-instance protocol copy

    print("DHT11")

    while True:
        # read() returns uint16: high byte = humidity %, low byte = temperature °C.
        # 0xFFFF signals a failure (no sensor, timeout or checksum mismatch).
        data: uint16 = sensor.read()

        if data == 0xFFFF:
            print("ERR")
            led.low()
        else:
            humidity:    uint8 = uint8(data >> 8)
            temperature: uint8 = uint8(data & 0xFF)
            print("H:", humidity, " T:", temperature, sep="")
            led.high()

        delay_ms(2000)
