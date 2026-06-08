# pymcu-avr

AVR (ATmega/ATtiny) backend for the PyMCU compiler. Free and open source.

Bundles the `pymcuc-avr` AOT binary that reads `.mir` IR files and emits AVR assembly,
then drives the AVR toolchain to produce a flashable Intel HEX file.

## Pipeline

```
pymcuc --emit-ir        →  firmware.mir    (target-agnostic IR)
pymcuc-avr (this pkg)   →  firmware.asm    (AVR assembly)
avr-as / avra           →  firmware.hex    (Intel HEX)
```

## Installation

```bash
pip install pymcu-avr
```

The AVR toolchain (`avr-gcc`, `avr-as`, `avr-objcopy`) is sourced (in order) from the
`pymcu-avr-toolchain` wheel cache, common system install paths, or `PATH`. A system
`avr-gcc` also works with no extra package:

```bash
# macOS
brew tap osx-cross/avr && brew install avr-gcc

# Debian/Ubuntu
apt install gcc-avr binutils-avr avr-libc
```

## Supported targets

Families: `atmega`, `attiny`, `at90`, `atxmega`.  
Examples: ATmega328P, ATmega2560, ATmega32U4, ATtiny85.

## Layout

```
src/python/pymcu/backend/avr/     backend plugin — wraps pymcuc-avr
src/python/pymcu/toolchain/avr/   AVR toolchain driver (assemble → HEX)
src/csharp/lib/                   AvrBackendProvider + codegen
src/csharp/cli/                   pymcuc-avr runner CLI
src/csharp/debugserver/           GDB-stub debug server
src/csharp/profiler/              cycle-accurate profiler
```

## Status

Alpha (API) / stable (codegen). 700+ integration tests green across ATmega328P,
ATmega2560, ATmega32U4, ATtiny85.
