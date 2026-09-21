# justfile — pymcu-avr build/test orchestration
# Requires: just (brew install just), dotnet >=10

set shell := ["bash", "-c"]

repo_root := justfile_directory()

# ─── Default ────────────────────────────────────────────────────────────────
default:
    @just --list

# ─── test-oracle ────────────────────────────────────────────────────────────
# Language oracle corpus: every probe under tests/oracle/probes runs once under
# CPython and once as compiled firmware on the AVR8Sharp emulator, and the two
# outputs are compared line by line. The suite runs under BOTH compiler front
# ends -- the C# parser and PYMCU_PY_PARSER=1 -- because a probe that only one
# front end accepts is a front-end bug, not a language result.
test-oracle:
    dotnet build "{{repo_root}}/tests/oracle/runner/PyMCU.OracleRunner.csproj" \
        -c Release -o "{{repo_root}}/build/oracle" --nologo
    "{{repo_root}}/.venv/bin/python" -m pytest "{{repo_root}}/tests/oracle" -q
    PYMCU_PY_PARSER=1 "{{repo_root}}/.venv/bin/python" -m pytest "{{repo_root}}/tests/oracle" -q
