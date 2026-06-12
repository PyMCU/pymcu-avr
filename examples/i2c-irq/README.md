# i2c-irq

Interrupt-driven I2C **peripheral** (TWI slave) at address 0x42.

## What it does

Configures the TWI hardware as an I2C peripheral and registers an ISR with
`i2c.irq(handler)` (no `@interrupt` decorator or manual `TWIE`/`SEI` writes).
The ISR runs the TWI state machine — inspecting `TWSR`, ACKing each event, and
saving received data bytes to a plain module global (auto-promoted to a `GPIOR`
register by the compiler) for the main loop to print as hex.

> The ISR **must** re-arm the interrupt by writing `TWCR` with `TWINT=1`
> (`0xC4 = TWINT|TWEA|TWEN`) or the peripheral stalls.

## Hardware

- Arduino Uno as I2C peripheral @ 16 MHz
- **SDA = PC4 (A4)**, **SCL = PC5 (A5)** (driven by TWI hardware)
- An I2C controller (another Arduino, Raspberry Pi, …) on the bus
- Serial terminal at **9600 baud**

## Expected output

`I2CI` banner, then two hex digits + newline per byte received from the controller.

## Key concepts

- TWI peripheral mode + interrupt-driven state machine
- ISR-shared plain global between ISR and main — auto-promoted to a `GPIOR` register

## Build & flash

```bash
cd examples/avr/i2c-irq
pymcu build
pymcu flash --port /dev/cu.usbmodemXXXX
```
