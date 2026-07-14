"""Shared fixtures for the Python integration suite.

These tests compile PyMCU firmware with ``pymcu build`` and run it against the ``avr8sharp``
package — the Python bindings over the same Avr8Sharp emulator the C#/NUnit suite uses. They
are skipped automatically if either prerequisite is missing.
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).parent))

avr8sharp = pytest.importorskip(
    "avr8sharp",
    reason="install the avr8sharp wheel into the repo .venv to run the Python integration suite",
)

import pymcu_compiler  # noqa: E402
from sim_session import SimSession  # noqa: E402


@pytest.fixture(scope="session")
def blink_hex() -> str:
    """Compiled HEX for examples/blink (built once per session)."""
    return pymcu_compiler.build("blink")


@pytest.fixture
def blink_session(blink_hex: str):
    """A fresh-reset Arduino Uno running the blink firmware."""
    session = SimSession(blink_hex)
    sim = session.reset()
    yield sim
    session.close()
