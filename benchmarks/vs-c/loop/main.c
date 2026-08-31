/* Same program as loop/src/main.py: a 64-iteration accumulation over a runtime
 * seed, with a conditional subtraction inside the loop so it cannot be reduced
 * to a closed form, then PB5 driven from bit 8 of the total. */
#include <avr/io.h>
#include <stdint.h>

int main(void) {
    DDRB |= (1 << PB5);
    for (;;) {
        uint8_t seed = GPIOR0;
        uint16_t total = 0;
        for (uint8_t i = 0; i < 64; i++) {
            total = total + (uint16_t) i * (uint16_t) seed;
            if ((total & 0x0080) != 0)
                total = total - 7;
        }
        if ((total & 0x0100) != 0)
            PORTB |= (1 << PB5);
        else
            PORTB &= (unsigned char) ~(1 << PB5);
    }
}
