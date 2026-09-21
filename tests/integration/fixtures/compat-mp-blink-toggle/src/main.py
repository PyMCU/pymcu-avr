# The canonical blink from pymcu.org's landing page and release-smoke history
# (RFC 0006, Section 12): the website's own source of truth for the number,
# verbatim from ~/Repos/website-copy's Playground.astro widget. Pinned
# here by name so the RFC's byte-identical gate has something concrete besides
# examples/blink (which is a different, HAL-native program at 150 B).
# 138 B since the constant-delay counter narrowed to the width the count needs
# (sleep_ms(500) fits in 24 bits); the website's "142 bytes" quote predates it.
#
# MicroPython machine.Pin, a single instance, single-instance fold applies.
from machine import Pin
import time

led = Pin(13, Pin.OUT)

while True:
    led.toggle()
    time.sleep_ms(500)
