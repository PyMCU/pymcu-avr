# stopwatch

A three-interrupt stopwatch: start/stop, reset, and a timer tick.

## What it does

Runs three simultaneous ISRs:

- **INT0 (PD2)** — start/stop toggle (falling edge)
- **INT1 (PD3)** — reset (falling edge)
- **TIMER0_OVF** — ~16.384 ms tick (61 ticks ≈ 1 second)

The LED is on while running; elapsed seconds are sent over UART as a raw byte +
newline. Each ISR signals main through its own plain module global — all three
are auto-promoted to `GPIOR0/1/2` by the compiler.

## Hardware

- Arduino Uno @ 16 MHz
- Start/Stop button on **PD2**, Reset button on **PD3** (active-low, pull-up)
- LED on **PB5** (D13)
- Serial terminal at **9600 baud**

## Key concepts

- Three concurrent interrupt sources
- ISR-shared plain globals for ISR ↔ main communication — auto-promoted to `GPIOR` registers

## Build & flash

```bash
cd examples/avr/stopwatch
pymcu build
pymcu flash --port /dev/cu.usbmodemXXXX
```
