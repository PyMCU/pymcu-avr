# Language oracle (AVR)

`tests/oracle/` is the permanent differential test suite for the PyMCU
language surface: each probe under `probes/` is a small top-level-statement
program that is run two ways -- once directly under CPython, once compiled
with `pymcu build` and executed in the `avr8sharp` `ArduinoUno` emulator
(UART0 captured until the program prints `END`) -- and the two outputs are
compared line by line. `test_oracle.py` is the pytest runner, parametrized
one test per probe file.

The corpus lives in this repository (moved out of the `PyMCU` compiler repo,
where the 2026-06 split left it) because every probe needs both halves of
the product: the compiler front ends *and* this repo's `pymcuc-avr` backend
plus the AVR emulator. Probe `# doc:` citations (`docs/language/...`) resolve
against the `PyMCU` checkout, where the language documentation lives; see
`docs/language/oracle.md` there for the header grammar, the divergence-citation
registry, and the running log of what the oracle has caught.

## Running

```sh
# build the emulator-side runner (Avr8Sharp.TestKit console app)
dotnet build tests/oracle/runner/PyMCU.OracleRunner.csproj -c Release -o build/oracle --nologo

# then the suite, once per front end
.venv/bin/python -m pytest tests/oracle -q          # C# front end
PYMCU_PY_PARSER=1 .venv/bin/python -m pytest tests/oracle -q   # Python front end
```

Environment overrides:

- `PYMCU_BIN` -- the `pymcu` driver binary to compile with
  (default: this repo's `.venv/bin/pymcu`, which resolves `pymcuc` through
  the sibling PyMCU checkout's editable driver install and the backend
  through this repo's editable `pymcu.backend.avr` plugin).
- `PYMCU_BACKEND_BINARY` -- e.g. `avr=/path/to/pymcuc-avr`, to pin a backend
  binary other than the plugin's own `build/bin/pymcuc-avr`.
- `PYMCU_ORACLE_RUNNER` -- path to a prebuilt `PyMCU.OracleRunner.dll`
  (default: builds `tests/oracle/runner` into `build/oracle/` itself).

Every probe is meant to run -- and stay green -- under both front ends.
`# tracked: #<N>` probes are known, filed compiler bugs and report as
`xfail(strict)`: a fix turns them into a hard XPASS failure until the
header is removed.

## Measured cells still waiting for a probe

These were measured during the 2026-09-26 construction-by-context sweep and are recorded
here because a measurement that lives only in a scratchpad is lost work. **None of them has
a probe yet**; each row is a real program that was compiled and run, with the result the
emulator gave against CPython.

Every program seeds its values from `GPIOR0.value` rather than a literal, because with a
literal the constant folder evaluates the call and the cell measures the folder instead of
the construction. Three cells in this sweep looked healthy for exactly that reason.

### Receiver that is not a name bound to an object

`o.m()` where `o` is a temporary. Four receiver shapes, five positions. Each program also
contains the bound-receiver control and **two temporaries of different values**, so a slot
shared between them would show as two equal numbers rather than as a silent pass.

| receiver | value | condition | argument | index | in a function |
|---|---|---|---|---|---|
| `Src(...).get()`, a constructor call | ok | ok | ok | ok | ok |
| `h.inner.get()`, a field | ok | ok | ok | ok | **[#520](https://github.com/PyMCU/PyMCU/issues/520)** |
| `make(2).get()`, a factory's result | refused | refused | refused | refused | refused |
| `lst[i].get()`, an element of a list of instances | ok | ok | ok | ok | ok |

The factory row is a real refusal, loud and consistent across all five positions:

- `'.get()' cannot be dispatched: its receiver is not a name bound to an object, a register,
  or a value PyMCU defines methods on.`

**Corrected after this table was first written.** The last row was recorded as refused in all
five positions and that was wrong. The refusal came from the sweep's own control line,
`bound = lst[i0]`, which binds a list element to a name; that spelling is refused with
`'lst' is not addressable at run time here: it lives as separate variables, so it can only be
indexed with a constant`, and so is a field read `lst[i0].base`. The construct the row was
named for, a METHOD call at a runtime index, compiles and is correct. Probes `429` and `438`
pin the two halves.

A control that shares a line with the construct under test is not a control, which is the
same lesson as retyping a file instead of copying it: both move two things at once.

**Read the first four columns narrowly.** They are all module-level reads; the fifth is the
only one that reads from inside a real subroutine, which is why it is the only one that
found anything. A probe for this row should vary the reading scope as well as the position.

### Positions that came back clean

Five constructions (a method call, `len()`, a subscript, a conditional expression and
arithmetic) were each placed as a subscript index, as an f-string interpolation and as a
ternary arm, with the plain spelling printed beside it as the control: all twenty agreed.
`len()` was also taken out of `print()` into six other positions, all correct. Probes
`420`-`424` cover these.

### Comprehensions outside module scope

The corpus had 24 comprehensions at module level, one in a method and none in a function.
Measured with the single-clause, unfiltered form (the supported one), values seeded from
`GPIOR0` and all three elements distinct so an all-zeros result would show:

| scope | result |
|---|---|
| module level | ok |
| inside a plain function | ok |
| inside a method | ok |
| inside an `@inline` function | ok |
| inside an `@inline` method | ok |

Four positions refuse, identically at module and at function scope: as a field, as a return
value, as a call argument, and as a `for`-in iterable. The diagnostics are

- `a list comprehension is only supported where it fills a fixed array whose length is a
  compile-time constant (xs: uint8[4] = [f(i) for i in range(4)]). In this position there is
  no array ...`
- `for-in loop iterable must be a compile-time string constant, a constant list literal
  [v0, v1, ...], range(N), enumerate(list/range), zip(a, b), reversed(iterable), or a
  fixed-array slice`

**Two spellings that look alike behave differently.** `[x * 10 + y for x in [1, 2] for y in
[3, 4]]`, two `for` clauses in one comprehension, still miscompiles silently: probe `063`
re-measured verbatim prints `0, 0, 0, 0` where CPython prints `13, 14, 23, 24`
([PyMCU#394](https://github.com/PyMCU/PyMCU/issues/394), still open). A comprehension nested
inside a comprehension, `[[...] for ...]`, refuses loudly instead. #394's title covers only
the first.

### Comparisons and f-strings in the positions that had none

| construction and position | result |
|---|---|
| comparison as a value, as an argument, as a field, as an index, in a condition | ok |
| comparison as a function return | prints `0`/`1`, see below |
| chained comparison in a condition, as an argument, as an index | ok |
| chained comparison as a value | prints `1`/`0`, see below |
| f-string as a function return | refused |
| f-string as an instance field | refused |
| `len()` bound to a name, in a condition, as an argument, as an index, as a return, interpolated | ok (probe `424`) |

Both f-string refusals are the documented one: supported streamed to a sink and assigned to a
name, refused in other expression positions. Probe `423` pins the ternary-arm case.

The `0`/`1` rows are [PyMCU#386](https://github.com/PyMCU/PyMCU/issues/386): the same boolean
prints as `False` when written straight into `print()`, read from a field, or negated, and as
`0` when it arrives through a name binding, a function return, or an `or`. Six positions, one
program, two representations. `docs/language/type-system.md:20` says `True`/`False` fold to
`1`/`0`, so three of the six follow the documentation and three follow CPython.
