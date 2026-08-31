/* C equivalent of pymcu-avr/examples/dht-sensor/src/main.py.
 *
 * Matched feature for feature against the Python, because the C side is the half
 * nobody audits:
 *   UART(9600)                 -> uart_init at 16 MHz, 8N1
 *   print("DHT11")             -> uart_str + newline
 *   DHT11(D2).read()           -> dht_read on PD2: 18 ms low start, release,
 *                                 40 bits timed off the high-pulse width,
 *                                 checksum verified, 0xFFFF on any failure
 *   print("H:", h, " T:", t)   -> uart_str + unsigned decimal conversion
 *   print("ERR")               -> uart_str
 *   led.high() / led.low()     -> PB5
 *   delay_ms(2000)             -> _delay_ms(2000)
 *
 * Deliberately NOT used: printf (it would drag in ~1.5 KB of libc formatting and
 * would be an unfair comparison in the other direction). The decimal conversion
 * is hand-written, which is what an embedded C author would do here.
 */
#include <avr/io.h>
#include <util/delay.h>
#include <stdint.h>

#define DHT_BIT   PD2

static void uart_init(void) {
    UBRR0H = 0;
    UBRR0L = 103;                       /* 9600 baud @ 16 MHz, U2X0 = 0 */
    UCSR0B = (1 << TXEN0);
    UCSR0C = (1 << UCSZ01) | (1 << UCSZ00);
}

static void uart_tx(char c) {
    while (!(UCSR0A & (1 << UDRE0))) { }
    UDR0 = (unsigned char) c;
}

static void uart_str(const char *s) {
    while (*s) uart_tx(*s++);
}

static void uart_u8(uint8_t v) {
    char buf[4];
    uint8_t i = 0;
    if (v == 0) { uart_tx('0'); return; }
    while (v) { buf[i++] = (char) ('0' + (v % 10)); v /= 10; }
    while (i) uart_tx(buf[--i]);
}

/* High byte = humidity, low byte = temperature; 0xFFFF on failure. */
static uint16_t dht_read(void) {
    uint8_t bytes[5] = {0, 0, 0, 0, 0};
    uint8_t i, b;
    uint16_t count;

    DDRD |= (1 << DHT_BIT);             /* drive low for 18 ms */
    PORTD &= (unsigned char) ~(1 << DHT_BIT);
    _delay_ms(18);
    PORTD |= (1 << DHT_BIT);            /* release, pull-up takes it high */
    _delay_us(30);
    DDRD &= (unsigned char) ~(1 << DHT_BIT);

    count = 0;                          /* sensor pulls low: response start */
    while (PIND & (1 << DHT_BIT)) { if (++count > 1000) return 0xFFFF; }
    count = 0;
    while (!(PIND & (1 << DHT_BIT))) { if (++count > 1000) return 0xFFFF; }
    count = 0;
    while (PIND & (1 << DHT_BIT)) { if (++count > 1000) return 0xFFFF; }

    for (i = 0; i < 40; i++) {
        count = 0;                      /* 50 us low precedes every bit */
        while (!(PIND & (1 << DHT_BIT))) { if (++count > 1000) return 0xFFFF; }
        count = 0;                      /* 26 us = 0, 70 us = 1 */
        while (PIND & (1 << DHT_BIT)) { if (++count > 1000) return 0xFFFF; }
        b = (count > 40) ? 1 : 0;
        bytes[i >> 3] = (uint8_t) ((bytes[i >> 3] << 1) | b);
    }

    if ((uint8_t) (bytes[0] + bytes[1] + bytes[2] + bytes[3]) != bytes[4])
        return 0xFFFF;
    return (uint16_t) (((uint16_t) bytes[0] << 8) | bytes[2]);
}

int main(void) {
    uint16_t data;

    uart_init();
    DDRB |= (1 << PB5);
    uart_str("DHT11\n");

    for (;;) {
        data = dht_read();
        if (data == 0xFFFF) {
            uart_str("ERR\n");
            PORTB &= (unsigned char) ~(1 << PB5);
        } else {
            uart_str("H:");
            uart_u8((uint8_t) (data >> 8));
            uart_str(" T:");
            uart_u8((uint8_t) (data & 0xFF));
            uart_tx('\n');
            PORTB |= (1 << PB5);
        }
        _delay_ms(2000);
    }
}
