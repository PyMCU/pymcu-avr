# PyMCU -- inline-register-arg: an @inline parameter carries the argument of ITS OWN call.
#
# Regression for PyMCU#144. Passing a whole-register read REG.value to an @inline function
# bound that function's parameter to the register itself, through constantAddressVariables,
# and nothing cleared the binding when the SAME @inline was expanded again at a later call
# site: the parameter key is the inline prefix plus the name, and that key is reused across
# call sites at the same depth. So every later call re-read the register and ignored its own
# argument. Clean build, no diagnostic, plausible-looking numbers.
#
# What it cost: printing a peripheral register next to a counter, which is what anyone
# inspecting hardware does, printed the register twice. An I2C scan that printed TWBR and then
# the number of devices found printed the TWBR value both times.
#
# The second half covers the other face of the same binding: a parameter DECLARED uint8 that
# receives a register read was aliased to the address rather than copied, so arithmetic on it
# was rejected with "'v' names a register, not its contents" -- a message about the argument,
# pointing at a parameter the program never wrote as a register.
#
# GPIOR0 is zero out of reset and is only here to defeat constant folding: a literal argument
# would measure the folder instead of the binder.
#
# Expected UART output:
#   r=0
#   x=1
#   i=1
#   j=8
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.uart import UART
from pymcu.types import uint8, inline


@inline
def show(u: UART, v: uint8):
    u.print_byte(v + 1)


def main():
    u = UART(9600)
    x: uint8 = GPIOR0.value + 1

    # The register first, then a local. The local used to print the register again.
    u.write_str("r=")
    u.print_byte(GPIOR0.value)
    u.write_str("x=")
    u.print_byte(x)

    # A user @inline whose parameter is declared uint8 and does arithmetic on it. The first
    # call passes the register, the second a local: 0+1 then 7+1.
    n: uint8 = GPIOR0.value + 7
    u.write_str("i=")
    show(u, GPIOR0.value)
    u.write_str("j=")
    show(u, n)

    u.write_str("done\n")
    while True:
        pass


main()
