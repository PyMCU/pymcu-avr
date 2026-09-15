# Regression fixture for PyMCU/PyMCU#407: Pin.pulse_in()'s measuring loop takes 9 cycles
# per iteration, not the 8 its count-to-microseconds conversion assumed, so every reading
# came back about 11% short. HC-SR04 wiring: Trig on D5 (PD5), Echo on D2 (PD2).
#
# Driven by tests/integration-py/test_pulse_in_hcsr04.py with avr8sharp.hc_sr04_echo at
# four distances; the printed tenths-of-a-centimeter value must land within 1 of the true
# pulse width converted the same way CPython would (us * 17 // 100).
from pymcu.hal.gpio import Pin
from pymcu.hal.uart import UART
from pymcu.time import delay_us, delay_ms
from pymcu.types import uint16

trig = Pin("PD5", Pin.OUT, value=0)
echo = Pin("PD2", Pin.IN)
uart = UART(9600)

while True:
    trig.high()
    delay_us(10)
    trig.low()
    us: uint16 = echo.pulse_in(1, timeout_us=30000)
    tenths_cm: uint16 = us * 17 // 100
    uart.print_uint16(tenths_cm)
    delay_ms(100)
