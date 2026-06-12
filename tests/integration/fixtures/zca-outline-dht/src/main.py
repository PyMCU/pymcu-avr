# RFC 0001 Model A on a DHT-style driver -- the BLOAT case the user cares about.
#
# This driver is written the NATURAL way: the full bit-bang read protocol lives
# directly in DHT.read(self), using the instance's runtime field self.pin. There is
# NO manual "thin @inline dispatch + shared runtime worker" split (the trick the
# shipped dht11.py uses to avoid bloat by hand).
#
# With @outline, the compiler shares one DHT_read body across all three sensors --
# they each call it with their own pin. Flip @outline -> @inline and the entire
# protocol is duplicated three times in flash. That delta is the whole point.
#
# No sensor is attached in simulation, so the ACK wait times out and read() returns
# 0xFFFF for every pin; the test asserts the shared body ran for each instance and
# that exactly one DHT_read label exists.
from pymcu.types import uint8, uint16, inline
from pymcu.chips.atmega328p import DDRD, PORTD, PIND
from pymcu.time import delay_ms, delay_us
from pymcu.hal.uart import UART


@inline
def _wait(mask: uint8, level: uint8) -> uint8:
    timeout: uint8 = 255
    while timeout > 0:
        current: uint8 = PIND.value & mask
        if level == 0:
            if current == 0:
                return timeout
        else:
            if current:
                return timeout
        timeout = timeout - 1
    return 0


@inline
def _byte(mask: uint8) -> uint8:
    result: uint8 = 0
    bit: uint8 = 0
    while bit < 8:
        if _wait(mask, 0) == 0:
            return 0
        if _wait(mask, 1) == 0:
            return 0
        count: uint8 = 0
        while PIND.value & mask:
            count = count + 1
            if count == 255:
                break
        result = result << 1
        if count > 35:
            result = result | 1
        bit = bit + 1
    return result


class DHT:
    def __init__(self, pin: uint8):
        self.pin = pin

    # Written naturally: the whole protocol is here, keyed on self.pin. @outline makes
    # the compiler emit ONE shared body taking self_pin as a parameter.
    def read(self) -> uint16:
        mask: uint8 = 1 << self.pin

        DDRD.value = DDRD.value | mask
        PORTD.value = PORTD.value & ~mask
        delay_ms(18)
        DDRD.value = DDRD.value & ~mask
        PORTD.value = PORTD.value | mask
        delay_us(40)

        if _wait(mask, 0) == 0:
            return 0xFFFF
        if _wait(mask, 1) == 0:
            return 0xFFFF

        hum_int: uint8 = _byte(mask)
        hum_dec: uint8 = _byte(mask)
        temp_int: uint8 = _byte(mask)
        temp_dec: uint8 = _byte(mask)
        chksum: uint8 = _byte(mask)

        expected: uint8 = (hum_int + hum_dec + temp_int + temp_dec) & 0xFF
        if chksum != expected:
            return 0xFFFF

        result: uint16 = hum_int
        result = (result << 8) | temp_int
        return result


def main():
    uart = UART(9600)
    uart.println("DHT")

    a = DHT(2)
    b = DHT(3)
    c = DHT(4)

    r1: uint16 = a.read()
    r2: uint16 = b.read()
    r3: uint16 = c.read()

    # All error out (no sensor): low byte of each is 0xFF -> write 0xFF thrice.
    uart.write(uint8(r1 & 0xFF))
    uart.write(uint8(r2 & 0xFF))
    uart.write(uint8(r3 & 0xFF))

    while True:
        pass
