# type-checking-guard-import: PyMCU/PyMCU#480 / #481.
#
# The Adafruit motor/servo TYPE_CHECKING guard:
#   try:
#       from pwmio import PWMOut
#   except NotImplementedError:
#       from circuitpython_typing.pwmio import PWMOut
#
# WHAT DISCRIMINATES: prints 7. Unknown type PWMOut would not build.
# Loading the stub handler would fail (no circuitpython_typing in this fixture).
from pymcu.time import delay_ms
from sensor import Thing, PWMOut

p = PWMOut()
p.n = 7
t = Thing(p)


def main():
    while True:
        print(t.val())
        print("END")
        delay_ms(1200)
