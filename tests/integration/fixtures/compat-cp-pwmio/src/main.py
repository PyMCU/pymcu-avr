# CircuitPython pwmio integration test fixture
#
# Verifies that pwmio.PWMOut:
#   - Configures Timer0 in Fast PWM mode (TCCR0A bits)
#   - Writes the correct duty cycle to OCR0A (32768 is 128 of 256 counts high, OCR0A 127)
#
# After PWM setup, sends 0x44 ('D') via busio.UART to signal completion.
#
import board
import busio
from pwmio import PWMOut


def main():
    uart = busio.UART(board.TX, board.RX, baudrate=9600)
    # 50 % duty cycle: 32768 is 128 counts high, and fast PWM is high for OCR + 1, so OCR0A = 127
    pwm = PWMOut(board.D6, duty_cycle=32768)
    uart.write(b"D")  # 'D' done marker
    while True:
        pass
