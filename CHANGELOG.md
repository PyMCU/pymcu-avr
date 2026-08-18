# Changelog — pymcu-avr

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
