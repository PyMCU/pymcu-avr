# machine.UART(0, 115200) has to reach the same registers the native UART(115200)
# reaches: U2X0 double speed with UBRR=16, which is 115942 baud, +0.64%.
#
# The twin of fixtures/compat-cp-uart-115200-u2x, for the other compat layer. Both
# layers carry the rate through a parameter of declared width, and 115200 does not fit
# a uint16: a layer that declares one hands the HAL 49664 and the part is configured
# for 50000 baud with nothing said.
#
# What let that happen was not the declaration. It was that the native path had a test
# and the layers did not, so the only thing that ever disagreed with the hardware was
# something no suite looked at. CircuitPython got caught because a buffer test lost
# received bytes; MicroPython was broken in silicon at the same time and every suite
# stayed green. Whoever adds the next compat layer should pin its registers here too.
#
# Sends 'U' at 115200 after init.
from machine import UART


def main():
    uart = UART(0, 115200)
    uart.write("U")
    while True:
        pass
