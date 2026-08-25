# PyMCU -- factory-returns-class: a function may return a multi-field class instance.
#
# Regression for PyMCU#49. A single-field class travels back from a non-@inline function in a
# register (RFC 0001 Model B) and a slot class travels as a pointer to its SRAM slot. Every
# multi-field class, which is every HAL class, has neither: the call returned a scalar, the
# name it was assigned to never learned its class, and the next method call became
#
#   error: CompileError: call to undefined function 'led_value'
#
# naming a function the program never wrote. Such a function has no standalone form at all and
# is now expanded at its call sites, so the construction happens there.
#
# Two pins from the same factory, on two ports, so a single shared expansion that ignored its
# argument would be visible: PB5 and PC1 are set as outputs and driven to opposite levels.
#
# Checkpoint via BREAK: directions configured and both pins driven.
from pymcu.hal.gpio import Pin
from pymcu.types import asm, const


def make_output(name: const[str]) -> Pin:
    return Pin(name, Pin.OUT)


def main() -> None:
    led = make_output("PB5")
    other = make_output("PC1")

    led.value(1)
    other.value(0)

    asm("BREAK")
    while True:
        pass


main()
