"""Discover and compile PyMCU projects for testing.

Walks up from a test file to the nearest PyMCU project (a ``pyproject.toml`` with a
``[tool.pymcu]`` table), runs ``pymcu build``, and returns the resulting Intel HEX. Results are
cached per project + source fingerprint so a monorepo with many projects compiles each at most
once per session, and only rebuilds when sources change.
"""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
import tomllib
from pathlib import Path

# project dir -> (fingerprint, hex). Module-level so it survives across fixtures in a session.
_CACHE: dict[Path, tuple[str, str]] = {}


def find_project(start: Path) -> Path:
    """Returns the nearest ancestor directory of ``start`` that is a PyMCU project."""
    start = start.resolve()
    candidates = [start, *start.parents] if start.is_dir() else list(start.parents)
    for d in candidates:
        pyproject = d / "pyproject.toml"
        if pyproject.is_file() and _has_pymcu_table(pyproject):
            return d
    raise FileNotFoundError(
        f"No PyMCU project (pyproject.toml with [tool.pymcu]) found at or above {start}."
    )


def _has_pymcu_table(pyproject: Path) -> bool:
    try:
        with pyproject.open("rb") as f:
            return "pymcu" in tomllib.load(f).get("tool", {})
    except (OSError, tomllib.TOMLDecodeError):
        return False


def read_target(project: Path) -> str:
    """Returns the ``[tool.pymcu] target`` chip name for a project."""
    with (project / "pyproject.toml").open("rb") as f:
        pymcu = tomllib.load(f).get("tool", {}).get("pymcu", {})
    target = pymcu.get("target")
    if not target:
        board = pymcu.get("board")
        if board:
            return board  # board alias; resolved downstream by avr8sharp.board()
        raise KeyError(f"[tool.pymcu] in {project} declares neither 'target' nor 'board'.")
    return target


def _pymcu_cmd() -> list[str]:
    # Prefer the `pymcu` installed in the SAME environment as the running interpreter (the venv
    # where pymcu-test is installed) — not whatever happens to be first on PATH.
    sibling = Path(sys.executable).parent / ("pymcu.exe" if os.name == "nt" else "pymcu")
    if sibling.exists():
        return [str(sibling)]
    exe = shutil.which("pymcu")
    if exe:
        return [exe]
    return [sys.executable, "-m", "pymcu"]


def _fingerprint(project: Path) -> str:
    """A cheap content fingerprint: newest mtime across pyproject + source tree."""
    newest = (project / "pyproject.toml").stat().st_mtime
    with (project / "pyproject.toml").open("rb") as f:
        sources = tomllib.load(f).get("tool", {}).get("pymcu", {}).get("sources", "src")
    src_dir = project / sources
    if src_dir.is_dir():
        for p in src_dir.rglob("*"):
            if p.is_file():
                newest = max(newest, p.stat().st_mtime)
    return f"{newest:.6f}"


def build(project: Path) -> str:
    """Compiles ``project`` with ``pymcu build`` (cached) and returns its firmware HEX."""
    project = project.resolve()
    fp = _fingerprint(project)
    cached = _CACHE.get(project)
    if cached and cached[0] == fp:
        return cached[1]

    proc = subprocess.run(
        [*_pymcu_cmd(), "build"],
        cwd=str(project),
        capture_output=True,
        text=True,
        timeout=180,
        env=dict(os.environ),
    )
    if proc.returncode != 0:
        raise RuntimeError(
            f"`pymcu build` failed in {project} (exit {proc.returncode}):\n"
            f"{proc.stdout}\n{proc.stderr}"
        )

    hex_file = project / "dist" / "firmware.hex"
    if not hex_file.is_file():
        raise FileNotFoundError(f"Firmware HEX not found after build: {hex_file}")

    hex_content = hex_file.read_text()
    _CACHE[project] = (fp, hex_content)
    return hex_content
