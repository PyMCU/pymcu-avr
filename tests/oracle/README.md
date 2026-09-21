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
