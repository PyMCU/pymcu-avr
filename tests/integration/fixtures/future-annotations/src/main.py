# future-annotations: PyMCU/PyMCU#452.
#
# `from __future__ import annotations` is a compiler pragma that enables nothing
# here: annotations are already read from the source. CircuitPython libraries
# (adafruit_irremote among them) write it unguarded at the top of the file.
#
# Before this, DependencyGraphBuilder tried to LOAD __future__ like any
# third-party module and refused with "Module not found: __future__", so this
# program did not build at all.
#
# The discriminating value is 9, add(8, 1). A program that still refused the
# import would not reach END; a typed function that dropped its body would
# not print 9.
#
# Expected UART output:
#   9
#   END
from __future__ import annotations
from pymcu.types import uint8


def add(x: uint8, y: uint8) -> uint8:
    return x + y


def main() -> None:
    print(add(8, 1))
    print("END")
    while True:
        pass
