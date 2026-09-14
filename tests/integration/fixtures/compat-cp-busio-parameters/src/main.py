# CircuitPython busio: the parameters reach the hardware (pymcu-circuitpython#22, #23, #24).
#
# bits, parity, stop, frequency, polarity and phase were accepted by busio and thrown away.
# A UART asked for 7E2 ran 8N1, a bus asked for 400 kHz ran at 100, and a display asked for
# mode 3 at 1 MHz ran mode 0 at 4 MHz. All three were silent. Read back at each BREAK:
#
#   break  register          expected
#   1      GPIOR0 = UCSR0C   0x2C  UPM=10 (even), USBS=1 (two stop), UCSZ=10 (seven bits)
#   2      GPIOR0 = TWBR     12    400 kHz at 16 MHz; it was 72, which is 100 kHz
#   3      GPIOR0 = SPCR     0x5D  SPE|MSTR|CPOL|CPHA|SPR=01 -> mode 3 at fosc/16 = 1 MHz
#          GPIOR1 = SPSR     0x00  SPI2X clear
import board
from busio import UART, I2C, SPI, Parity
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, UCSR0C, TWBR, SPCR, SPSR
from pymcu.types import asm


def main():
    uart = UART(board.TX, board.RX, baudrate=9600, bits=7,
                parity=Parity.EVEN, stop=2, receiver_buffer_size=1)
    GPIOR0.value = UCSR0C.value
    asm("BREAK")

    i2c = I2C(board.SCL, board.SDA, frequency=400000)
    GPIOR0.value = TWBR.value
    asm("BREAK")

    spi = SPI(board.SCK, MOSI=board.MOSI, MISO=board.MISO)
    spi.configure(baudrate=1000000, polarity=1, phase=1)
    GPIOR0.value = SPCR.value
    GPIOR1.value = SPSR.value
    asm("BREAK")

    while True:
        pass
