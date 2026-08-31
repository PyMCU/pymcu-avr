/* Same program as arith/src/main.py: read GPIOR0 as a runtime seed, do the same
 * four 16-bit operations in the same order, and drive PB5 from bit 8 of the
 * result. GPIOR0 is used on both sides so the arithmetic cannot be folded. */
#include <avr/io.h>
#include <stdint.h>

int main(void) {
    DDRB |= (1 << PB5);
    for (;;) {
        uint8_t seed = GPIOR0;
        uint16_t acc = (uint16_t) seed * 37 + 11;
        acc = acc ^ (uint16_t) (acc >> 5);
        acc = acc + (uint16_t) seed * 3;
        if ((acc & 0x0100) != 0)
            PORTB |= (1 << PB5);
        else
            PORTB &= (unsigned char) ~(1 << PB5);
    }
}
