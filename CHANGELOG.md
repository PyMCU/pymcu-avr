# Changelog — pymcu-avr

## 0.1.0b1 — Unreleased (prepared 2026-09-15)

Beta 1: the AVR backend moves out of alpha alongside the frontend
(`pymcu-compiler`/`pymcu-stdlib` 0.1.0b1) and the CircuitPython layer.
Every fix below was exercised on real Arduino Uno silicon or the AVR8Sharp
emulator with a regression fixture in the integration suite. The
ARM/RP2040/RP2350, PIC, and RISC-V backends stay alpha on purpose.

### Added

- **avr**: the catalog speaks for itself -- a devices subcommand dumping what the backend uses
- **avr**: FlashSize joins the catalog -- the backend's last name list dies
- **avr**: float modulo, with Python's floored semantics
- **avr**: the flash and SRAM sizes come from the .mir, and the catalog loses them

### Fixed

- **avr**: the chip says its own geometry -- stop deducing it from the name
- **avr**: float() of a 32-bit integer reads all four bytes
- **avr**: a float cast to uint32 uses the unsigned helper
- **avr**: a fused divmod refuses the fusion when both results share a register
- **avr**: an array larger than 256 bytes addresses all of it
- **avr**: a flash table larger than 256 bytes addresses all of it
- two programs asked UART for baud 0 and got 1 Mbaud
- **avr**: the build harness kills what it spawned, and stops timing out under load
- **avr**: each suite run gets its own scratch, so two runs stop deleting each other
- **avr**: isr-reg-preservation shared a subroutine between main and its ISR
- **avr**: an 8-bit register read in a wider context stops reading its neighbour
- **avr**: a backend refusal says where it happened, and stops calling a multiply a comparison
- **avr**: the call-stack reservation scales with the part
- **avr**: a uint32 compared against a threshold above 2147483647 emits no comparison at all
- **build**: reference PyMCU at its real case, which a case-sensitive filesystem needs
- **ci**: check the monorepo out as PyMCU, the case the project references use
- **ci**: the rest of the monorepo paths, not only the checkout
- **test**: the chip catalog path is the third surface of the casing change
- **ci**: the dependency install paths, the fourth surface of the same casing change
- **ci**: the toolchain smoke job stopped dying at collection, and now has to prove it ran
- **ci**: guard all four pymcu imports in test_chip_lists_agree, not just the named one
- **ci**: guard the module-level USES too, and set the smoke floor to the measured number
- **ci**: the smoke command is one line, because PowerShell has no backslash continuation
- **ci**: the smoke floor is per platform, because Windows legitimately runs fewer
- **avr**: @extern calls use the C ABI for their first 32-bit argument
- **toolchain**: install the toolchain the user agreed to, not a different one
- **avr**: the unhandled-exception name derives from the shared list ([#260](https://github.com/PyMCU/pymcu-avr/issues/260))
- **avr**: a global an ISR writes is not homed in a callee-saved register ([PyMCU#328](https://github.com/PyMCU/PyMCU/issues/328))
- **avr**: the unhandled-exception path enables the transmitter itself ([#340](https://github.com/PyMCU/pymcu-avr/issues/340))
- **avr**: an ISR prologue saves every R2-R15 home its body writes ([#22](https://github.com/PyMCU/pymcu-avr/issues/22))
- **avr**: a call into the soft-float runtime keeps the T flag the exception model signals with ([PyMCU#384](https://github.com/PyMCU/PyMCU/issues/384))
- **avr**: a float whose only use is a comparison is still a float ([PyMCU#388](https://github.com/PyMCU/PyMCU/issues/388))

### Performance

- **avr**: a float's second operand is loaded straight into its ABI register

### Documentation

- **toolchain**: the native link path has no coverage, and cannot link float math

### Tests

- **avr**: pin byte concatenation to 16 bits
- **avr**: the ISR lands on its hardware vector slot -- checked in the linked binary
- **toolchain**: four chip lists, one arbiter
- **avr**: a free function that takes a class instance, in silicon terms
- **avr**: the __main__ guard runs the entry point once, not twice
- **avr**: both spellings of an aliased builtin reach the builtin
- **avr**: a pin driven from a runtime value, in both directions
- **avr**: bare stdlib imports, checked by result
- **avr**: sleep(0.5) is the same image as delay_ms(500)
- **avr**: the pin-name rejection names both routes that take a number
- **avr**: narrowing an unannotated literal keeps its value
- **avr**: module-level widths, and the divergence they uncover
- **avr**: IN_PULLUP on the registers, any() as the same image
- **avr**: the unexported-name message, both halves
- **avr**: the run-time index message, and that its advice works
- **avr**: input() in a cast is the two statements it desugars to
- **avr**: the missing-board message names the line to add
- **avr**: two names bound to two functions, and the refusal of a third case
- **avr**: format() output, not just its image
- **avr**: pins[i] drives the selected pin and only that one
- **avr**: the Python front end must produce the same image, corpus-wide
- **avr**: the pins declared above are the pins that get driven
- **avr**: the concatenation is the f-string, image for image
- **avr**: the bytes of a b"..." literal, read back from the chip
- **avr**: the bare yield suspends three times and the splice passes three arguments
- **avr**: the text reaches the UART as text
- **avr**: the inherited fields and the declared defaults arrive
- **avr**: counter-in-loop -- a method that updates a field, called in a loop
- **avr**: while-condition-call -- the method that drives the loop from its condition
- **avr**: held-instance-field -- an object reading the field of the object it holds
- **avr**: outline-mutating-getter -- the write an @outline getter used to lose
- **avr**: the two programs @outline used to refuse to build
- **avr**: with-context-manager -- both spellings in one program
- **avr**: instance-list-loop -- for over a name bound to a list of instances
- **avr**: ctor-calls-own-method -- the write a constructor's own call used to lose
- **avr**: signed-mixed-width -- three signed answers checked against CPython
- **avr**: comprehensions over names, and a string indexed at run time
- **avr**: yield-from -- four shapes of generator delegation
- **avr**: array-runtime-init -- the elements an array used to drop
- **avr**: async-loop-local -- the accumulator a coroutine's for loop used to lose
- **avr**: str-class-field -- the string a field used to print as 256
- **avr**: async-sleep-float -- the fractional sleep that never woke
- **avr**: `in` over a named list, and the field of a returned instance
- **avr**: bool-protocol -- __bool__ in every position that asks for a truth value
- **avr**: async-create-task -- two tasks with different periods
- **avr**: method-instance-param -- the operand a shared body could not receive
- **avr**: outline-dunders -- four dunders that @outline used to break
- **avr**: module-instance-field -- the write a module-level object used to lose
- **avr**: module-instance-read -- the read side and the write-through-a-method side
- **avr**: pin today's wrong answers for PyMCU#116 and PyMCU#117
- **avr**: float-not-shifted -- the float a power-of-two rewrite used to change
- **avr**: imported-class -- a class in its own file
- **avr**: the folded condition and the module array that lost its bytes
- **avr**: adc-two-pins -- two AnalogPins keep their own channel
- **avr**: float-modulo -- the four sign combinations and the one that needs exactness
- **avr**: int32-floor -- the value of int32's minimum, not just that it builds
- **avr**: overload-facade -- an overloaded constructor reached through a facade
- **avr**: loop-else and builtin-bool -- two constructs that used to be rejected
- **avr**: board pin numbers, list fields and the unrolled range
- **avr**: inline-register-arg, the @inline parameter that re-read a register
- **avr**: flash-table-wide, the const table that was never emitted
- **avr**: pin-irq-triggers and buffer-param -- two silent wrong answers
- **avr**: float-div-zero, the infinity that printed as a plausible zero
- **avr**: uart-baud-computed and uart-baud-8mhz, the divisor that stayed zero
- **avr**: imported-module-init, the top level that never ran
- **avr**: const-or-branch -- the `or` that did not link without the optimizer
- **avr**: factory-returns-class, the Pin a factory could not hand back
- **avr**: #117's failure moved twice, so the pin says which one it is now
- **avr**: inline-multi-return, the prescaler that was always 1
- **avr**: str-runtime-branch and str-runtime-module, the seed that changed no answer
- **avr**: a run-time PWM duty of 0 leaves the compare output disconnected
- **compat**: give compat-mp-pwm-freq a duty, so non-inverting means something
- **avr**: the import diagnostics moved to the import, so the pins follow
- **avr**: a Timer1 PWM duty must not commit a stale TEMP byte
- **avr**: import-global-split, the declared value that shipped unreachable
- **avr**: module-array-name-collision, the buffer that shrank to someone else's size
- **avr**: module-instance-nonmain, the pin a function above main could not drive
- **avr**: the .mir decides LPM against ELPM, and a missing size stops the build
- **avr**: async-gather-shadowed-names, the coroutine whose name was a class
- **avr**: math-rounding, the nine values where floor, ceil and trunc disagree
- **avr**: register width, with the neighbour seeded and both sides of the cap
- **avr**: a refusal carries its source position and names the operator
- **avr**: the stack stays inside its reservation, and both sentinels retire
- **avr**: both known IR-optimizer divergences are gone
- **avr**: a write through a held instance survives, in both construction shapes
- **avr**: a hand-written state machine loses its later arm, pinned as it stands today
- **avr**: the state machine advances, and a coroutine wakes from its await
- **avr**: two coroutines each keep their own object
- **avr**: the imported-module pin fixture asks for a name that exists
- **avr**: the debug spelling prints its label on silicon
- **avr**: a module-level accumulator does not wrap
- **avr**: a join whose separator lives in a field, at one level and at two
- **avr**: min and max with a key, checked against CPython on silicon
- **avr**: a one-character string equals itself, on silicon
- **avr**: 'in' and 'match' over a one-character string, on silicon
- **avr**: a string copied to another name prints its text
- **avr**: float-local-overload, the local that spelled itself uint8
- **avr**: base-call-returns-class, the base body that vanished
- **avr**: a float local is not truncated
- **avr**: a dict is walked, checked line by line against CPython
- **avr**: dict-one-char-key, the lookup that silently returned its default
- **avr**: the float local above a byte was wrapped, not just truncated
- **avr**: calling through the class object, checked against CPython
- **avr**: an ALL-CAPS global that is written keeps its value in silicon
- **avr**: the folded value and the computed value agree on silicon
- **avr**: the threshold at the top of each width, and the two spellings that contradicted
- **avr**: follow the unrolled-array diagnostic to its new wording
- **avr**: the direct alarm.sleep_until_alarms() call, beside its own wrappers ([#271](https://github.com/PyMCU/pymcu-avr/issues/271))
- **avr**: range counters sized from their bounds, on the emulator ([PyMCU#284](https://github.com/PyMCU/PyMCU/issues/284), [PyMCU#286](https://github.com/PyMCU/PyMCU/issues/286))
- **avr**: the loop variable after a range loop holds Python's value ([PyMCU#285](https://github.com/PyMCU/PyMCU/issues/285))
- **avr**: reversed(range()), x in range(), enumerate(range()) and a stepped comprehension ([PyMCU#287](https://github.com/PyMCU/PyMCU/issues/287), [PyMCU#288](https://github.com/PyMCU/PyMCU/issues/288))
- **avr**: a single-field instance mutated in a method's loop, and the method's value ([PyMCU#292](https://github.com/PyMCU/PyMCU/issues/292))
- **avr**: pwmio.PWMOut.frequency reprograms the timer, and is refused without variable_frequency ([pymcu-circuitpython#5](https://github.com/PyMCU/pymcu-circuitpython/issues/5))
- **avr**: the inverting PWM mode, and the machine.PWM spellings MicroPython programs use ([PyMCU#293](https://github.com/PyMCU/PyMCU/issues/293), [pymcu-micropython#6](https://github.com/PyMCU/pymcu-micropython/issues/6))
- **avr**: the ADC-to-PWM programs use MicroPython's duty(adc.read()) idiom, and the two-channel PWM fixture is timing-dependent ([pymcu-micropython#6](https://github.com/PyMCU/pymcu-micropython/issues/6))
- **avr**: a named tuple past the unroll limit, and the width its table is stored at ([PyMCU#297](https://github.com/PyMCU/PyMCU/issues/297), [PyMCU#298](https://github.com/PyMCU/PyMCU/issues/298))
- **avr**: stop() keeps the timer and deinit() releases the pin ([PyMCU#296](https://github.com/PyMCU/PyMCU/issues/296))
- **avr**: the named-tuple fixture reads the high byte too, not just the value ([PyMCU#298](https://github.com/PyMCU/PyMCU/issues/298))
- **avr**: a Timer0 PWM next to the time base leaves the clock at its rate ([PyMCU#295](https://github.com/PyMCU/PyMCU/issues/295))
- **avr**: the surface fixture's second channel moves to Timer2 ([PyMCU#300](https://github.com/PyMCU/PyMCU/issues/300))
- **avr**: main()'s body runs where the call is written ([PyMCU#301](https://github.com/PyMCU/PyMCU/issues/301))
- **avr**: the 16-bit duty lands exactly, and every compare value moves one down ([pymcu-circuitpython#30](https://github.com/PyMCU/pymcu-circuitpython/issues/30))
- **avr**: a returned @inline call carries its callee's value ([PyMCU#302](https://github.com/PyMCU/PyMCU/issues/302))
- **avr**: an input has the pull it asked for, through the HAL and through digitalio ([PyMCU#309](https://github.com/PyMCU/PyMCU/issues/309))
- **avr**: a method on the `with ... as` name reaches the manager ([PyMCU#305](https://github.com/PyMCU/PyMCU/issues/305))
- **avr**: pull = None is no pull ([PyMCU#306](https://github.com/PyMCU/PyMCU/issues/306))
- **avr**: a driver takes a list of pins, of numbers, or a buffer
- **avr**: compat-cp-analogio-full-scale, the 16-bit value and the channel a read selects
- **avr**: busio parameters, the UART receive ring, and the board bus constructors
- **avr**: the seven-segment acceptance program, written as a user writes it
- **avr**: pulse capture, an infrared frame end to end, and the gated carrier
- **avr**: the servo idiom end to end, and the nearest-bucket rule moves to Timer0
- **avr**: a bit-banged I2C transfer decoded off the two pins ([#10](https://github.com/PyMCU/pymcu-avr/issues/10))
- **avr**: colorwheel's three corners and the ramp between two of them ([#15](https://github.com/PyMCU/pymcu-avr/issues/15))
- **avr**: every falling edge on the pin is counted ([#11](https://github.com/PyMCU/pymcu-avr/issues/11))
- **avr**: buttons pressed one and two at a time, drained one event a call ([#13](https://github.com/PyMCU/pymcu-avr/issues/13))
- **avr**: a sub-millisecond sleep, a watchdog that turns off, and the part's EEPROM size
- **avr**: which of two alarms fired, and that a time alarm fires at all ([#20](https://github.com/PyMCU/pymcu-avr/issues/20))
- **avr**: a literal time.sleep() folds to calibrated loops, no 32-bit helpers ([pymcu-circuitpython#26](https://github.com/PyMCU/pymcu-circuitpython/issues/26))
- **avr**: an unannotated comprehension now reads right at run time
- **avr**: examples/pwm-multi is timing-dependent, like the other free-running PWM programs
- **avr**: compat-cp-board-buses is back, with its feature ([#7](https://github.com/PyMCU/pymcu-avr/issues/7))
- **avr**: compat-cp-rotaryio, a knob whose position actually moves ([#12](https://github.com/PyMCU/pymcu-avr/issues/12))
- **avr**: a delay computed into locals takes the calibrated loop ([PyMCU#327](https://github.com/PyMCU/PyMCU/issues/327))
- **avr**: a MicroPython library compiled unmodified, dicts and all
- **avr**: a seven-segment table keyed by characters
- **avr**: the mask table next to the row table, same glyphs
- **avr**: an unhandled raise in main prints its name and stops ([PyMCU#339](https://github.com/PyMCU/PyMCU/issues/339))
- **avr**: the scaffolded ATtiny85 CircuitPython blink builds and drives its pin
- **avr**: neopixel_write's waveform is read back as the bytes it sent
- **avr**: a raise in a program with no print still names itself ([#340](https://github.com/PyMCU/pymcu-avr/issues/340))
- **avr**: the WS2812 waveform is measured against the datasheet, not against itself
- **avr**: the raise-no-print fixture blinks, so the differential corpus can read it
- **avr**: a load retires the constant the name held ([#359](https://github.com/PyMCU/pymcu-avr/issues/359))
- **avr**: **kwargs and *args as compile-time forms, through PyMCU and through the machine layer ([#368](https://github.com/PyMCU/pymcu-avr/issues/368))
- **avr**: a bounded exception object carries its message and its type ([#369](https://github.com/PyMCU/pymcu-avr/issues/369))
- **avr**: the Adafruit example shape, measured as text on the wire ([PyMCU#374](https://github.com/PyMCU/PyMCU/issues/374), [#375](https://github.com/PyMCU/PyMCU/issues/375))
- **avr**: a handler that writes a home register cannot change what main holds there ([#22](https://github.com/PyMCU/pymcu-avr/issues/22))
- **avr**: where a printed line's side effects happen ([PyMCU#371](https://github.com/PyMCU/PyMCU/issues/371))
- **avr**: a module-level float keeps its value, in both modules and every spelling ([PyMCU#379](https://github.com/PyMCU/PyMCU/issues/379))
- **avr**: a raise reaches its handler after a float has been printed ([PyMCU#384](https://github.com/PyMCU/PyMCU/issues/384))
- **avr**: a layer namespace bound to a local survives a loop ([PyMCU#259](https://github.com/PyMCU/PyMCU/issues/259))
- **avr**: a user's import does not move a layer function's parameters ([PyMCU#381](https://github.com/PyMCU/PyMCU/issues/381))
- **avr**: a class attribute through an instance, and a descriptor ([#268](https://github.com/PyMCU/pymcu-avr/issues/268), [#360](https://github.com/PyMCU/pymcu-avr/issues/360))
- **avr**: a module buffer grown by .extend() takes the largest size ([#362](https://github.com/PyMCU/pymcu-avr/issues/362))
- **avr**: a field holding an instance is true or false the way its class says ([PyMCU#385](https://github.com/PyMCU/PyMCU/issues/385))
- **avr**: every operator that asks a field for a truth value asks its class ([PyMCU#385](https://github.com/PyMCU/PyMCU/issues/385))
- **avr**: a name is as wide as the value finally stored in it ([PyMCU#385](https://github.com/PyMCU/PyMCU/issues/385))
- **avr**: every float relation is decided by its operands ([PyMCU#388](https://github.com/PyMCU/PyMCU/issues/388))
- **avr**: a float operand is not shuffled through the first argument's registers

### Build

- a missing sibling repository fails first and says what to do

### CI

- dispatch a playground rebuild after a successful PyPI publish
- **integration**: test the toolchain users actually get, and say what that drops

### Reverted

- **test**: compat-cp-board-buses, whose feature is reverted ([#7](https://github.com/PyMCU/pymcu-avr/issues/7))

### Other

- Revert tracking of the ffi-wide-args fixture, which was not mine to commit
- Untrack the ffi-wide-args fixture again, swept in by a directory-wide add
- bench(avr): PyMCU against avr-gcc -Os, four programs, one instrument


## 0.1.0a9 — 2026-08-18

Hardware-validation release: every fix below was found or verified on a real
Arduino Uno with a logic analyzer, and each one ships with a regression
fixture in the integration suite (1549 tests).

### Codegen fixes
- Float-to-integer conversion stored the `__fixsfsi` result with a MOV pair
  that clobbered the high word before reading it: a 32-bit destination got
  the low word duplicated (`uint32(3.25 * 100.0 + 0.5)` stored 0x01450145).
  Both conversion sites now swap register pairs via MOVW.
- Float comparisons route through `__cmpsf2` for all six relations, and float
  negation flips the sign bit (both previously reused integer sequences over
  clobbered registers).
- Constants wider than 16 bits widen the whole operation (a folded 32-bit
  constant divided at 16 bits and truncated silently).
- The peephole keeps live-out temps at outlined-region RETs (unoptimized
  builds lost the region result).
- The outliner's identity check sees nested inline regions; linear-scan live
  intervals extend across loop back-edges (a loop bound register was reused
  mid-loop).

### Toolchain / limits
- The linker script declares real MEMORY regions per chip, so `ld` itself
  refuses an oversized image; static SRAM overflow is now a codegen error
  with the chip's real numbers instead of a runtime stack collision.

### Test surface
- New fixtures pin: two's-complement wraparound at every width, the
  exception-model edges, overload resolution with `const[...]` parameters,
  PWM/tone/servo timer maps, the 115200 U2X0 divisor, both PWM channels of a
  shared timer, `uint32(float)` truncation, the CPython `bytearray` repr for
  nvm slices, `str.join`, runtime-bounds slice iteration, and value-returning
  methods on nested ZCA fields.
