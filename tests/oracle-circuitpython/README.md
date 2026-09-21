# CircuitPython oracle (SSD1306, three-way)

`tests/oracle-circuitpython/` boots the real CircuitPython 10.3.1 firmware for
the Raspberry Pi Pico on the RP2040Sharp emulator and proves that three
independent executions of the same SSD1306 programs emit the same I2C
transaction stream, byte for byte:

1. **CPython** -- the fixture's `oracle/oracle.py` runs the vendored library
   sources under stock CPython against a fake I2C bus and prints every
   transaction (the reference stream, same as the AVR oracle tests use).
2. **CircuitPython 10.3.1 on RP2040Sharp** -- the same vendored `.py` sources
   installed into the emulated Pico's CIRCUITPY drive, the same program run as
   `code.py` on the real interpreter.
3. **PyMCU AVR firmware** -- the fixture compiled by this repo's backend and
   run on AVR8Sharp's emulated Arduino Uno.

The fixtures are reused in place from `tests/integration/fixtures/`
(`adafruit-ssd1306-unmodified`, `compat-cp-framebuf-text`); nothing is copied.
Today that is 50 transactions for `ssd1306_simpletest` and 43 for the
framebuf-text program, identical on all three sides.

## Why a separate project

The AVR integration suite must not pull in the Pico emulator, so this is its
own NUnit project rather than part of `tests/integration`. It also needs the
**RP2040Sharp repository checked out as a sibling** of this repo -- the csproj
references `../../../RP2040Sharp/src/RP2040Sharp` and `src/RP2040.TestKit`
directly (the same convention `pymcu-arm` uses for RP2350Sharp). If that
checkout is missing the project does not build.

## What the harness does

- `FirmwareCache` downloads
  `adafruit-circuitpython-raspberry_pi_pico-en_US-10.3.1.uf2` from
  downloads.circuitpython.org into `~/.cache/pymcu-oracle/` (override with
  `PYMCU_ORACLE_CACHE`) and verifies its MD5
  (`327b526a211c5d78151f9aa3e119e550`) on every use. The UF2 is never committed.
- `CircuitPythonPico` then prepares a **2 MiB flash snapshot** once per machine:
  boot the UF2, inject a `boot.py` running `storage.disable_usb_drive()` into
  the FAT12 CIRCUITPY partition (so the filesystem stays writable despite the
  USB host), reboot into the modified image, and write the fixture library
  files (`lib/adafruit_ssd1306.py`, `lib/adafruit_framebuf.py`,
  `lib/adafruit_bus_device/*`, `font5x8.bin`) over the USB-CDC REPL. The
  snapshot is cached next to the UF2 with a stamp hashing every input, so a
  fixture edit rebuilds it. It is never committed.
- Per program: a fresh `PicoSimulation` on the snapshot gets `code.py` written
  over the REPL, then CTRL-D autoruns it and the wire is recorded.

### The one source change the Pico forces

The `raspberry_pi_pico` build of CircuitPython defines no default I2C bus, so
`board.I2C()` does not exist there. The harness rewrites the fixture's
`main.py` into `code.py` with exactly one change:

```python
i2c = board.I2C()                     # fixture / AVR / CPython oracle
i2c = busio.I2C(board.GP1, board.GP0) # code.py on the Pico (SCL=GP1, SDA=GP0)
```

plus the `import busio` that needs. Everything else -- driver sources,
framebuffer content, transaction order -- is identical by construction.

## The recorder: two bus paths, one stream

`Ssd1306WireRecorder` merges two recording paths because CircuitPython itself
splits the bus in two:

- Ordinary `writeto`/`readfrom` calls go through the RP2040's DW_apb_i2c
  peripheral and are recorded via `I2cPeripheral`'s `DeviceResponds`,
  `OnWrite`, `OnRead`, and `OnStop` hooks.
- **Zero-length writes never reach the peripheral.** CircuitPython's
  `common_hal_busio_i2c_write` (`ports/raspberrypi/common-hal/busio/I2C.c`)
  says literally "The RP2040 I2C peripheral will not perform 0 byte writes. So
  use bitbangio.I2C to do the write" -- and `SSD1306_I2C.__init__` issues
  exactly such a bare-address probe. The recorder therefore also decodes the
  bitbang engine's GPIO edges on GP0 (SDA) / GP1 (SCL) off `SioPeripheral.
  OnGpioChanged`, and drives the slave ACK by forcing the SDA pad low with
  `GpioPin.ForceInput`.

Both paths append to the same ordered transaction list, so comparisons are
against the same `I2cTransaction` stream shape the AVR side produces.

The AVR side reuses the shared machinery in `tests/testkit/` (extracted from
`AdafruitSsd1306UnmodifiedTests`): `UnoTwiTrace` for the emulator run and
`I2cStreams.AssertEqual` for failure messages that name the first differing
transaction, the byte offset, and a short hex window -- never a whole-buffer
dump.

## Running

```sh
# requires: sibling checkout of RP2040Sharp, this repo's .venv with pymcu
dotnet test tests/oracle-circuitpython/PyMCU.OracleCircuitPython.csproj
```

Wall-clock on an M-series MacBook (RP2040 at its real 125 MHz):

- cold run (UF2 cached, snapshot rebuild): ~21 s
- warm run (snapshot cached): ~3 s total; each CircuitPython autorun ~1.2 s
  (boot to REPL ~0.45 s + library probe + program), each AVR emulator run
  ~30 ms, each `pymcu build` a few seconds.

`dotnet test` skips nothing offline: if the UF2 cannot be downloaded and is
not already cached, the fixture setup fails loudly -- an oracle that does not
run is not an oracle.
