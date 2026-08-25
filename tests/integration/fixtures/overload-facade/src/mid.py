# The facade. Its re-export is CONDITIONAL, which is how every HAL facade in the stdlib
# is written, and it is what stops the re-export chain from being followed to the module
# that defines the class.
from pymcu.chips import __CHIP__
from pymcu.exceptions import CompileError

if __CHIP__.arch == "avr":
    from impl import Low
else:
    raise CompileError("this fixture is AVR-only")
