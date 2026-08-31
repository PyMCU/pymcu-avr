# PyMCU vs avr-gcc -Os

    python3 benchmarks/vs-c/run.py

Four programs, each written twice: `src/main.py` and `main.c`, in the same directory, doing
the same thing. The runner builds both, measures both with one function, and prints the
toolchain provenance beside the numbers.

## What it answers, and what it does not

The two claims usually made about PyMCU are different claims, and only one of them is about
total binary size:

**"Zero-cost abstraction"** is about what an abstraction costs against writing the same thing
by hand. It holds, and it is visible in the disassembly rather than in this table:

    PyMCU  Pin(LED_BUILTIN, Pin.OUT)  ->  sbi 0x04, 5     bytes 25 9a
    C      DDRB |= (1 << PB5)         ->  sbi 0x04, 5     bytes 25 9a
    PyMCU  led.high()                 ->  sbi 0x05, 5     bytes 2d 9a
    C      PORTB |= (1 << PB5)        ->  sbi 0x05, 5     bytes 2d 9a

The class, the constructor and the method call cost nothing. That is the claim, and it is
byte-identical.

**"As small as C"** is a different claim and this table is how to check it. It is not
currently true for tight arithmetic. Saying both with one phrase is what invites the question
nobody has an answer to.

Neither figure here is speed. Every number is bytes.

## Why it is built the way it is

**One size instrument for both sides.** `hex_bytes()` counts program data out of the Intel HEX
and is the only thing that measures either side. Using the driver's reported figure for PyMCU
and `avr-size` for C would compare two definitions of "size", and the difference between the
definitions would look like a result.

**The `.c` sits beside the `.py`.** The C side is the half nobody audits, and a C program that
quietly does less than the Python one is the easiest way to produce a flattering number. A
measurement of this same example from June 2026 cannot be re-checked today because its C was
never kept.

**Provenance prints with the numbers**, read from the installed distribution metadata rather
than from `pymcu --version` -- those two disagree on this machine, and the metadata is what
actually produced the binary.

## Adding a program

A directory with `pyproject.toml`, `src/main.py` and `main.c`, then its name in `PROGRAMS`.
Make the two sides do the same work, and say in a comment where they deliberately differ --
`dht-sensor/main.c` avoids `printf` on purpose and says why.
