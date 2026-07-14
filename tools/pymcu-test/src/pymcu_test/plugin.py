"""pytest plugin: turnkey simulation testing for PyMCU projects.

Drop a ``test_*.py`` at or under a PyMCU project and write::

    def test_blinks(mcu):
        mcu.run_ms(50)
        assert mcu.port_b.pin_high(5)

The plugin auto-discovers the project (nearest ``pyproject.toml`` with ``[tool.pymcu]``), compiles
it with ``pymcu build``, selects the matching board from ``[tool.pymcu] target``, loads the
firmware, and resets to power-on before each test — so it works unchanged across the projects of
a monorepo. Requires the ``avr8sharp`` package and a working PyMCU toolchain.
"""

from __future__ import annotations

from pathlib import Path

import pytest

from . import compiler


def pytest_addoption(parser: pytest.Parser) -> None:
    group = parser.getgroup("pymcu")
    group.addoption(
        "--pymcu-project",
        action="store",
        default=None,
        help="Path to the PyMCU project to test (overrides per-test auto-discovery).",
    )
    parser.addini(
        "pymcu_project",
        help="Path to the PyMCU project to test (overrides per-test auto-discovery).",
        default=None,
    )


def _configured_project(request: pytest.FixtureRequest) -> Path | None:
    opt = request.config.getoption("--pymcu-project")
    ini = request.config.getini("pymcu_project")
    chosen = opt or ini
    if not chosen:
        return None
    p = Path(chosen)
    if not p.is_absolute():
        p = (request.config.rootpath / p).resolve()
    return p


@pytest.fixture
def pymcu_project(request: pytest.FixtureRequest) -> Path:
    """The PyMCU project for the current test.

    Uses ``--pymcu-project`` / the ``pymcu_project`` ini option when set, otherwise the nearest
    project at or above the test file (so each project in a monorepo resolves to its own)."""
    configured = _configured_project(request)
    if configured is not None:
        return compiler.find_project(configured)
    return compiler.find_project(Path(request.path).parent)


@pytest.fixture
def pymcu_target(pymcu_project: Path) -> str:
    """The ``[tool.pymcu] target`` chip name of the project under test."""
    return compiler.read_target(pymcu_project)


@pytest.fixture
def firmware(pymcu_project: Path) -> str:
    """Compiled firmware HEX for the project under test (cached per session)."""
    return compiler.build(pymcu_project)


@pytest.fixture
def mcu(pymcu_target: str, firmware: str):
    """A simulation of the project's target chip, with firmware loaded and reset to power-on.

    Yields an ``avr8sharp`` board (e.g. :class:`avr8sharp.ArduinoUno`). Closed after the test."""
    avr8sharp = pytest.importorskip(
        "avr8sharp", reason="install the avr8sharp package to run PyMCU simulation tests"
    )
    sim = avr8sharp.board(pymcu_target)
    sim.snapshot()          # capture peripheral power-on defaults before firmware
    sim.with_hex(firmware)
    sim.restore()           # clean power-on state for this test
    try:
        yield sim
    finally:
        sim.close()
