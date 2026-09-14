# PyMCU -- list-param-pins: a driver takes several pins as a list (PyMCU#313)
#
# Every line prints a PORT register, never pin.value(): reading the pin back lets the
# compiler fold the write and the read into the constant it just stored, so a firmware
# with no write to PORTD at all still printed the right number.
#
# `sel` comes from GPIOR0 (the test seeds 1) so the run-time selection is decided on the
# chip, not by the folder.
#
# Expected UART, seed 1:
#   A 0 / B 28 / C 0 / D 4 / E 3 / F 8 / G 96 / H 252 15 / I 0 0 / J 96
#   END
from pymcu.chips.atmega328p import GPIOR0, PORTB, PORTD
from pymcu.hal.console import print
from pymcu.hal.gpio import Pin
from pymcu.types import uint8


class Led:
    def __init__(self, pin):
        self._pin = pin

    def on(self):
        self._pin.value(1)


class Bar:
    def __init__(self, pins):
        self._pins = pins

    def all_on(self):
        for p in self._pins:
            p.value(1)

    def all_off(self):
        for p in self._pins:
            p.value(0)

    def first_on(self):
        self._pins[0].value(1)

    def one(self, i: uint8):
        self._pins[i].value(1)

    def count(self) -> uint8:
        return len(self._pins)


class Rack:
    def __init__(self, leds):
        self._leds = leds

    def all_on(self):
        for l in self._leds:
            l.on()


class Tag:
    def __init__(self, tag: uint8):
        self.tag = tag

    def light(self, pins):
        for p in pins:
            p.value(1)


wide = [Pin("PD2", Pin.OUT), Pin("PD3", Pin.OUT), Pin("PD4", Pin.OUT),
        Pin("PD5", Pin.OUT), Pin("PD6", Pin.OUT), Pin("PD7", Pin.OUT),
        Pin("PB0", Pin.OUT), Pin("PB1", Pin.OUT), Pin("PB2", Pin.OUT),
        Pin("PB3", Pin.OUT)]


def portd() -> uint8:
    return PORTD.value & 0xFC


def portb() -> uint8:
    return PORTB.value & 0x3F


def main():
    sel: uint8 = GPIOR0.value

    bar = Bar([Pin("PD2", Pin.OUT), Pin("PD3", Pin.OUT), Pin("PD4", Pin.OUT)])
    print("A", portd())
    bar.all_on()
    print("B", portd())
    bar.all_off()
    print("C", portd())
    bar.first_on()
    print("D", portd())
    bar.all_off()
    print("E", bar.count())

    bar.one(sel)
    print("F", portd())
    bar.all_off()

    rack = Rack([Led(Pin("PD5", Pin.OUT)), Led(Pin("PD6", Pin.OUT))])
    rack.all_on()
    print("G", portd())

    big = Bar(wide)
    big.all_on()
    print("H", portd(), portb())
    big.all_off()
    print("I", portd(), portb())

    t = Tag(9)
    t.light([Pin("PD5", Pin.OUT), Pin("PD6", Pin.OUT)])
    print("J", portd())

    print("END")


main()
