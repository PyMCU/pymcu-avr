/* Same program as blink/src/main.py: PB5 (Arduino D13) toggling at 1 Hz.
 * Pin(LED_BUILTIN, Pin.OUT) -> DDRB bit 5; led.high()/low() -> PORTB bit 5;
 * delay_ms(500) -> _delay_ms(500). No UART, no other peripherals, same as the
 * Python side. */
#include <avr/io.h>
#include <util/delay.h>

int main(void) {
    DDRB |= (1 << PB5);
    for (;;) {
        PORTB |= (1 << PB5);
        _delay_ms(500);
        PORTB &= (unsigned char) ~(1 << PB5);
        _delay_ms(500);
    }
}
