"""Compiles PyMCU AVR firmware via the ``pymcu build`` CLI and returns the Intel HEX.

Python port of ``tests/integration/PymcuCompiler.cs``. Results are cached in-process so each
program is compiled at most once per test session. Used by the Python integration suite, which
runs the compiled firmware against the ``avr8sharp`` package — the same emulator the C#/NUnit
suite uses, so tests can be written in either language.
"""

from __future__ import annotations

import hashlib
import os
import subprocess
from functools import lru_cache
from pathlib import Path

_VERBOSE = os.environ.get("PYMCU_VERBOSE") == "1" or os.environ.get("RUNNER_DEBUG") == "1"


def _find_repo_root() -> Path:
    """Walks up from this file to the pymcu-avr repo root (has hatch_build.py + examples/)."""
    for d in [Path(__file__).resolve(), *Path(__file__).resolve().parents]:
        if (d / "hatch_build.py").is_file() and (d / "examples").is_dir():
            return d
    raise FileNotFoundError(
        "Cannot locate pymcu-avr repo root (no hatch_build.py + examples/ in any parent)."
    )


REPO_ROOT = _find_repo_root()
_VENV_BIN = REPO_ROOT / ".venv" / "bin"
_VENV_PYTHON = _VENV_BIN / "python3"
_PYMCU = _VENV_BIN / "pymcu"


def fixture_dir(name: str) -> Path:
    """Absolute path of a compiler test fixture directory."""
    return REPO_ROOT / "tests" / "integration" / "fixtures" / name


@lru_cache(maxsize=None)
def build(name: str) -> str:
    """Compiles the showcase example at ``examples/{name}`` and returns its HEX."""
    return _compile(REPO_ROOT / "examples" / name, name)


@lru_cache(maxsize=None)
def build_fixture(name: str) -> str:
    """Compiles the compiler test fixture at ``tests/integration/fixtures/{name}``."""
    return _compile(fixture_dir(name), name)


@lru_cache(maxsize=None)
def build_source(main_py: str, target: str = "atmega328p", frequency: int = 16_000_000) -> str:
    """Compiles an arbitrary generated ``main.py`` into a throwaway project and returns its HEX."""
    digest = hashlib.sha1(main_py.encode("utf-8")).hexdigest()
    proj = Path(os.environ.get("TMPDIR", "/tmp")) / "pymcu-gen" / digest[:16]
    (proj / "src").mkdir(parents=True, exist_ok=True)
    (proj / "pyproject.toml").write_text(
        "[project]\n"
        'name = "gen"\n'
        'version = "0.1.0"\n'
        'requires-python = ">=3.11"\n\n'
        "[tool.pymcu]\n"
        f'target = "{target}"\n'
        f"frequency = {frequency}\n"
        'sources = "src"\n'
        'entry = "main.py"\n'
    )
    (proj / "src" / "main.py").write_text(main_py)
    return _compile(proj, "gen-" + digest[:8])


def _compile(project_dir: Path, name: str) -> str:
    if not project_dir.is_dir():
        raise FileNotFoundError(f"Project directory not found: {project_dir}")

    env = dict(os.environ)
    env["PATH"] = f"{_VENV_BIN}{os.pathsep}{env.get('PATH', '')}"
    if _VERBOSE:
        env["PYMCU_VERBOSE"] = "1"

    proc = subprocess.run(
        [str(_VENV_PYTHON), str(_PYMCU), "build"],
        cwd=str(project_dir),
        capture_output=True,
        text=True,
        timeout=120,
        env=env,
    )

    if proc.returncode != 0 or _VERBOSE:
        if proc.returncode != 0:
            print(f"[pymcu_compiler] build failed: {name} (dir {project_dir})")
        print(f"[pymcu_compiler] exit: {proc.returncode}")
        if proc.stdout.strip():
            print("stdout:\n" + proc.stdout)
        if proc.stderr.strip():
            print("stderr:\n" + proc.stderr)

    if proc.returncode != 0:
        raise RuntimeError(
            f"pymcu build failed for '{name}' (exit {proc.returncode}):\n{proc.stdout}\n{proc.stderr}"
        )

    hex_file = project_dir / "dist" / "firmware.hex"
    if not hex_file.is_file():
        raise FileNotFoundError(f"Firmware HEX not found after build: {hex_file}")
    return hex_file.read_text()
