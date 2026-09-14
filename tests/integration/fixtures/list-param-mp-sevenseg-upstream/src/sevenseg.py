"""
SevenSeg MicroPython Library
----------------------------

Author : Kritish Mohapatra
License: MIT License
Year   : 2025

Description:
    A simple MicroPython library for controlling a single-digit 7-segment display.
    Supports both Common Anode and Common Cathode types.
    Works with ESP32, ESP8266, Raspberry Pi Pico, and other MicroPython-compatible boards.

Usage Example:
    from sevenseg import SevenSeg
    from time import sleep

    pins = [15, 2, 4, 16, 17, 5, 18]  # Pins for segments a,b,c,d,e,f,g
    display = SevenSeg(pins, common_anode=False)

    for i in range(10):
        display.show(i)
        sleep(1)
    display.clear()
"""

from machine import Pin

class SevenSeg:
    """
    SevenSeg - Class to control a single-digit 7-segment display.

    Parameters:
        pins (list): GPIO pin numbers for segments [a, b, c, d, e, f, g].
        common_anode (bool): Set True for common anode displays; False for common cathode.
        dp_pin (int, optional): GPIO pin for the decimal point (DP). Default is None.
    """

    def __init__(self, pins, common_anode=False, dp_pin=None):
        # Validate number of segment pins
        if len(pins) != 7:
            raise ValueError("Need exactly 7 pins for segments a-g")

        self.common_anode = common_anode
        self.segments = [Pin(p, Pin.OUT) for p in pins]
        self.dp = Pin(dp_pin, Pin.OUT) if dp_pin else None

        # Segment pattern for digits 0-9 (a, b, c, d, e, f, g)
        self.digits = {
            0: [1,1,1,1,1,1,0],
            1: [0,1,1,0,0,0,0],
            2: [1,1,0,1,1,0,1],
            3: [1,1,1,1,0,0,1],
            4: [0,1,1,0,0,1,1],
            5: [1,0,1,1,0,1,1],
            6: [1,0,1,1,1,1,1],
            7: [1,1,1,0,0,0,0],
            8: [1,1,1,1,1,1,1],
            9: [1,1,1,1,0,1,1],
        }

        # Initialize display in clear state
        self.clear()

    # ---------------------------------------------------------
    def show(self, num):
        """
        Display a digit (0–9) on the 7-segment display.

        Args:
            num (int): The digit to display (0–9).
        """
        if num not in self.digits:
            raise ValueError("Digit must be between 0 and 9")

        pattern = self.digits[num]
        for pin, seg_on in zip(self.segments, pattern):
            # Adjust logic for common anode or cathode
            pin.value(seg_on ^ self.common_anode)

    # ---------------------------------------------------------
    def clear(self):
        """
        Turn off all segments and the decimal point (if present).
        """
        for pin in self.segments:
            pin.value(1 if self.common_anode else 0)

        if self.dp:
            self.dp.value(1 if self.common_anode else 0)

    # ---------------------------------------------------------
    def dot(self, on=True):
        """
        Control the decimal point (DP) LED.

        Args:
            on (bool): True to turn on DP, False to turn off.
        """
        if not self.dp:
            return
        self.dp.value(on ^ self.common_anode)

