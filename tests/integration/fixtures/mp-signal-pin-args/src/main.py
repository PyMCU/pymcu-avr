# machine.Signal built from Pin arguments, upstream's Signal(pin_arguments..., invert=...):
# Signal(13, Pin.OUT, invert=True) bound the Pin-object form with 13 as the pin and failed
# on an internal 'undefined s__pin__pin_low'. Also the Pin-object form with invert passed
# positionally, as upstream allows.
from machine import Pin, Signal
from pymcu.chips.atmega328p import PORTB, DDRB, GPIOR0

sg1 = Signal(13, Pin.OUT, invert=True)
sg1.on()
print(PORTB[5], DDRB[5], sg1.value())
sg1.value(GPIOR0.value)
print(PORTB[5], sg1.value())
sg2 = Signal(Pin(12, Pin.OUT), True)
sg2.on()
print(PORTB[4])
print("END")
