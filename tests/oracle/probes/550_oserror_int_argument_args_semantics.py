# expect: match
# doc: https://docs.pymcu.org/limitations/#exception-handling
# An integer raise argument is the exception's one argument: e.args, e.args[0]
# and len(e.args) are identical between CPython and PyMCU -- this half of the
# feature is not a divergence. The same for a raise propagated through a call
# and for OSError catching its TimeoutError subclass.
from pymcu.chips.atmega328p import GPIOR0

try:
    raise OSError(GPIOR0.value + 110)
except OSError as e:
    print(e.args)
    print(e.args[0])
    print(len(e.args))


def deeper():
    raise OSError(5)


try:
    deeper()
except OSError as e:
    print(e.args[0], len(e.args))

try:
    raise TimeoutError(11)
except OSError as e:
    print(e.args[0], len(e.args))

try:
    raise ValueError(7)
except ValueError as e:
    print(e.args[0])

print("END")
