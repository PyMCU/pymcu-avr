# machine.Pin symbolic-mode fixture -- twin of compat-mp-pin-mode-literals.
# Same program with the named constants where the twin writes the literal
# ints; both must compile to byte-identical images.
#
from machine import Pin
from pymcu.types import uint8


def on_btn(pin: Pin):
    pass


def main():
    out = Pin(13, Pin.OUT)                  # symbolic OUT
    inp = Pin(2, Pin.IN, Pin.PULL_UP)       # symbolic IN + pull-up
    aux = Pin("PD4", Pin.OUT)               # symbolic OUT, port-name form
    out.high()
    inp.irq(on_btn, Pin.IRQ_FALLING)        # symbolic IRQ_FALLING
    aux.init(Pin.IN)                        # symbolic IN: PD4 back to input
    while True:
        pass
