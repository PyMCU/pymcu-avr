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

The AVR toolchain (`avr-as`, `avr-ld`, `avr-objcopy`, `cc1`, `cc1plus`) ships with
`pymcu-avr` as `pymcu-avr-toolchain-wasi`: WebAssembly modules run through `wasmtime`, so
the same wheel works on Linux, macOS and Windows. No system `avr-gcc` is needed. The driver
falls back to a system `avr-gcc` on `PATH` only when the WASI package cannot be used.

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
tests/integration/                NUnit suite on the AVR8Sharp emulator
tests/testkit/                    shared I2C-oracle machinery (transaction
                                  stream, TWI recorder, oracle.py runner)
tests/oracle/                     language oracle corpus (184 probes, pytest)
tests/oracle-circuitpython/       three-way SSD1306 oracle: CircuitPython
                                  10.3.1 on RP2040Sharp vs CPython vs AVR
                                  firmware (needs an RP2040Sharp sibling
                                  checkout)
```

## Testing

`dotnet test tests/integration` runs the AVR8Sharp suite; `just test-oracle` runs the language oracle corpus (`tests/oracle`) under both compiler front ends.

## Status

Alpha (API) / stable (codegen). 700+ integration tests green across ATmega328P,
ATmega2560, ATmega32U4, ATtiny85.
