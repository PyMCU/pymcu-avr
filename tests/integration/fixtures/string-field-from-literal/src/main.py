# string-field-from-literal: adafruit_character_lcd's self._message = ""
# then the message setter. The first store is a str field, not uint8.
#
# WHAT DISCRIMINATES:
#   1  -- constructed and assigned a string through the setter
from pymcu.time import delay_ms


class Lcd:
    def __init__(self):
        self._message = ""

    @property
    def message(self):
        return self._message

    @message.setter
    def message(self, message: str):
        self._message = message


def main():
    while True:
        l = Lcd()
        l.message = "Hi"
        print(1)
        print("END")
        delay_ms(1200)
