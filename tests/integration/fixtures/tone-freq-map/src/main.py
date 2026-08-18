# ATmega328P: tone() prescaler buckets must respect the 8-bit OCR2A floor.
#
# In CTC toggle mode freq = F_CPU / (2 * N * (OCR2A + 1)), so each prescaler
# reaches DOWN to 31250 / N Hz at 16 MHz. The old thresholds were 4x too low:
# any request below a bucket's floor overflowed OCR2A, clamped to 255, and
# produced the bucket minimum instead -- tone(440) played 488 Hz, tone(1000)
# played 3906 Hz and tone(8000) played 31250 Hz, all measured on a real Uno.
#
#   tone(8000) -> prescaler 8   (TCCR2B=0x02), OCR2A = 124 -> 8000.0 Hz
#   tone(1000) -> prescaler 32  (TCCR2B=0x03), OCR2A = 249 -> 1000.0 Hz
#   tone(440)  -> prescaler 128 (TCCR2B=0x05), OCR2A = 141 -> 440.1 Hz
#
# Sends 'A' after tone(8000), 'B' after tone(1000), 'C' after tone(440).
from pymcu.hal.tone import tone
from pymcu.hal.uart import UART
from pymcu.time import delay_ms


def main():
    uart = UART(9600)

    tone(8000)
    uart.write('A')
    delay_ms(50)

    tone(1000)
    uart.write('B')
    delay_ms(50)

    tone(440)
    uart.write('C')

    while True:
        delay_ms(1000)
