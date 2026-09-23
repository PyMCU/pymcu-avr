# machine.Pin literal-mode fixture -- the constants' OTHER spelling.
#
# machine.Pin's constants hold the upstream MicroPython rp2 values (IN=0,
# OUT=1, IRQ_FALLING=4, IRQ_RISING=8), while every PyMCU GPIO HAL numbers its
# modes the other way around (IN=1, OUT=0). The layer translates at the HAL
# call, so a program that writes the literal ints upstream documents must build
# to the same image as the same program written with the named constants --
# compat-mp-pin-mode-symbols is that twin, and the test asserts their hex is
# byte-identical under both compiler front ends.
#
# Observable state after boot:
#   PB5 (D13): DDR set, driven high     -- Pin(13, 1) means OUT upstream
#   PD2 (D2):  DDR clear, PORT set      -- Pin(2, 0, PULL_UP) means IN + pull-up
#   PD4 (D4):  DDR set                  -- Pin("PD4", 1) means OUT upstream
#   EICRA ISC01:ISC00 = 10, EIMSK INT0  -- irq(..., 4) means FALLING upstream
#
from machine import Pin
from pymcu.types import uint8


def on_btn(pin: Pin):
    pass


def main():
    out = Pin(13, 1)                    # literal OUT
    inp = Pin(2, 0, Pin.PULL_UP)        # literal IN + pull-up
    aux = Pin("PD4", 1)                 # literal OUT, port-name form
    out.high()
    inp.irq(on_btn, 4)                  # literal IRQ_FALLING
    aux.init(0)                         # literal IN: PD4 back to input
    while True:
        pass
