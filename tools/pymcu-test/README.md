# pymcu-test

Turnkey pytest fixtures for simulating [PyMCU](https://github.com/PyMCU/pymcu) firmware on the
[avr8sharp](https://github.com/begeistert/avr8sharp) emulator — no hardware, no .NET runtime,
deterministic and fast.

## Install

```bash
pip install pymcu-test          # pulls in avr8sharp + pytest
```

## Use

Drop a `test_*.py` at or under a PyMCU project and use the `mcu` fixture:

```python
def test_blinks(mcu):
    mcu.run_ms(50)
    assert mcu.port_b.pin_high(5)        # PB5 / Arduino pin 13
```

The plugin auto-discovers the project (nearest `pyproject.toml` with `[tool.pymcu]`), compiles it
with `pymcu build`, picks the board from `[tool.pymcu] target`, loads the firmware, and resets to
power-on before each test. In a **monorepo** each test file resolves to its own project, so a
single `pytest` run validates every project from its own directory.

### Fixtures

| Fixture | What it gives you |
|---|---|
| `mcu` | The target board (`avr8sharp` simulation) with firmware loaded, reset per test |
| `firmware` | The compiled Intel HEX string |
| `pymcu_project` | `Path` to the resolved project |
| `pymcu_target` | The `[tool.pymcu] target` chip name |

### Peripherals

The `mcu` (an `avr8sharp` board) exposes GPIO, serial, ADC, and SPI/I²C device stubs:

```python
def test_reads_adc(mcu):
    mcu.adc.set_channel(0, 2.5)          # 2.5 V on A0
    mcu.run_ms(150)
    assert mcu.serial.bytes[-1] == 128   # ~half-scale

def test_finds_i2c_device(mcu):
    mcu.twi.set_slave(0x76, present=True)
    mcu.run_until_serial(mcu.serial, "Done", max_ms=4000)
    assert "FOUND 0x76" in mcu.serial.text
```

### Selecting a project explicitly

```bash
pytest --pymcu-project path/to/project
```

or in `pyproject.toml`:

```toml
[tool.pytest.ini_options]
pymcu_project = "firmware/my_project"
```

## License

Business Source License 1.1.
