#!/usr/bin/env python3
# surfacerun -- the sim half of a surfacecov fixture's oracle pair.
#
# oracle/oracle.py runs the fixture's src/main.py under stock CPython against
# fake CircuitPython modules (oracle_common.py); this script runs the same
# main.py compiled to firmware on the avr8sharp emulator against scripted
# slaves, and diffs the two outputs line for line.
#
# The fixture.json contract is shared with oracle_common.py:
#   {"i2c":[addrs], "spi":true, "read_script":"readscript.txt",
#    "pins_low":[...], "pins_high":[...], "pulse_reply":[...],
#    "pulse_frames":{...}, "onewire":{...}, "max_ms":N}
#
# Implemented here: i2c (a multi-address scripted slave sharing one response
# queue, matching _SCRIPT's global take()), spi (write-only stub: the slave
# has nothing to say back), pins_low/pins_high, read_script, max_ms.
# pulse_*/onewire fixtures are oracle-only for now -- see report-surfacecov.md.
#
# Usage:
#   python3 surfacerun.py <fixture-dir> [<fixture-dir> ...]
#   python3 surfacerun.py --all
#
# Needs the avr8sharp python binding; point PYTHONPATH at
# ~/Repos/avr8sharp/bindings/python/src and AVR8SHARP_LIBRARY at a current
# Avr8Sharp.Native build when it is not installed as a package.

import json
import os
import subprocess
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
_FIXTURES = os.path.normpath(os.path.join(_HERE, ".."))
_AVR8SRC = os.path.expanduser("~/Repos/avr8sharp/bindings/python/src")
if _AVR8SRC not in sys.path:
    sys.path.insert(0, _AVR8SRC)

import avr8sharp  # noqa: E402


def _fixture_dirs(args):
    if args == ["--all"]:
        out = []
        for name in sorted(os.listdir(_FIXTURES)):
            d = os.path.join(_FIXTURES, name)
            if name.startswith("surfacecov-") and os.path.exists(
                os.path.join(d, "fixture.json")):
                out.append(d)
        return out
    return [os.path.abspath(a) for a in args]


# Board pin name -> (port, bit) on the ATmega328P / Arduino Uno, matching the
# names the fixtures' pins_low/pins_high lists use.
def _pin(port_holder, name):
    if name.startswith("A"):
        return port_holder.port_c, int(name[1:])
    n = int(name[1:])
    if n < 8:
        return port_holder.port_d, n
    return port_holder.port_b, n - 8


def _read_script(fixture, cfg):
    path = cfg.get("read_script")
    if not path:
        return []
    return [int(tok, 16)
            for tok in open(os.path.join(fixture, path)).read().split()]


def _oracle_output(fixture):
    proc = subprocess.run(
        [sys.executable, os.path.join(fixture, "oracle", "oracle.py")],
        capture_output=True, text=True, timeout=120)
    return proc.stdout, proc.stderr, proc.returncode


def _sim_output(fixture, cfg):
    hex_path = os.path.join(fixture, "dist", "firmware.hex")
    sim = avr8sharp.ArduinoUno().with_hex(open(hex_path).read())

    for n in cfg.get("pins_low", []):
        port, bit = _pin(sim, n)
        port.set(bit, False)
    for n in cfg.get("pins_high", []):
        port, bit = _pin(sim, n)
        port.set(bit, True)

    for a in cfg.get("i2c", []):
        sim.twi.set_slave(int(a, 16) if isinstance(a, str) else a)
    script = _read_script(fixture, cfg)
    if cfg.get("i2c") and cfg.get("spi"):
        # The oracle feeds EVERY bus from the one shared take() script; wire the
        # sim the same way so an I2C read and a later SPI readinto see one
        # ordered stream (surfacecov-bus-device's MISO tail depends on it).
        sim.share_bus_responses()
        sim.twi.queue_response(*script)
    elif cfg.get("i2c"):
        sim.twi.queue_response(*script)
    elif cfg.get("spi"):
        sim.spi.queue_response(*script)

    max_ms = cfg.get("max_ms", 8000)
    # Stop at the completion marker when the program prints one; otherwise run
    # the budget (a firmware that halts or crashes just ends up quiet, and the
    # line diff below shows the divergence).
    try:
        sim.run_until_serial(sim.serial, "=DONE=\n", max_ms=max_ms)
    except Exception:
        pass
    sim.run_ms(20)
    return sim.serial.text


def _unsupported(cfg):
    return [k for k in ("pulse_reply", "pulse_frames", "onewire") if k in cfg]


# The compiler under test is the pair's editable pymcu, NOT whatever `pymcu`
# PATH happens to export (a pipx release would silently verify the wrong
# binary). Resolution order: $PYMCU, the pair checkout's .venv, then PATH.
def _pymcu_bin():
    env = os.environ.get("PYMCU")
    if env:
        return env
    pair_venv = os.path.join(_FIXTURES, "..", "..", "..", ".venv", "bin", "pymcu")
    pair_venv = os.path.normpath(pair_venv)
    if os.path.exists(pair_venv):
        return pair_venv
    return "pymcu"


def run_fixture(fixture, rebuild=False):
    cfg = json.load(open(os.path.join(fixture, "fixture.json")))
    name = os.path.basename(fixture)

    if (rebuild or cfg.get("expect_build_fail")
            or not os.path.exists(os.path.join(fixture, "dist", "firmware.hex"))):
        proc = subprocess.run([_pymcu_bin(), "build"], cwd=fixture,
                              capture_output=True, text=True)
        if proc.returncode != 0:
            tail = (proc.stderr or proc.stdout).strip()
            # A fixture can pin a KNOWN upstream-source refusal the way
            # known_divergence pins output lines: "expect_build_fail" carries a
            # substring of the compile error, so a refusal for the documented
            # reason reports XBUILD-FAIL while a new breakage still fails.
            ebf = cfg.get("expect_build_fail")
            if ebf and ebf in tail:
                return name, "XBUILD-FAIL", ebf
            return name, "BUILD-FAIL", "\n".join(tail.splitlines()[-8:])
        if cfg.get("expect_build_fail"):
            return name, "STALE-XFAIL", (
                "fixture declares expect_build_fail but compiles now -- "
                "remove the pin")

    expected, oerr, orc = _oracle_output(fixture)
    if orc != 0:
        return name, "ORACLE-FAIL", f"oracle exit {orc}\n{oerr}"

    unsupported = _unsupported(cfg)
    if unsupported:
        return name, "SIM-SKIP", f"no sim model for {', '.join(unsupported)}"

    try:
        actual = _sim_output(fixture, cfg)
    except Exception as e:
        return name, "SIM-CRASH", f"{type(e).__name__}: {e}"

    elines = expected.replace("\r\n", "\n").splitlines()
    alines = actual.replace("\r\n", "\n").splitlines()
    if elines == alines:
        return name, "MATCH", f"{len(elines)} lines identical"

    diff = []
    difflines = set()
    for i in range(max(len(elines), len(alines))):
        e = elines[i] if i < len(elines) else "<missing>"
        a = alines[i] if i < len(alines) else "<missing>"
        if e != a:
            diff.append(f"  line {i + 1}: oracle={e!r} sim={a!r}")
            difflines.add(i + 1)
    # A declared divergence is pinned to the lines it explains: an entry like
    # {"known_divergence": {"reason": "...", "lines": [30, 34]}} passes only
    # when the diff is EXACTLY that set -- a new line going wrong still fails
    # the gate, and a healed line makes the pin stale the same way.
    kd = cfg.get("known_divergence")
    if kd and difflines == set(kd.get("lines", [])):
        return name, "KNOWN-DIFF", kd["reason"] + "\n" + "\n".join(diff[:20])
    return name, "DIFF", "\n".join(diff[:20])


def main():
    args = [a for a in sys.argv[1:] if a != "--build"]
    rebuild = "--build" in sys.argv[1:]
    results = []
    for fixture in _fixture_dirs(args or ["--all"]):
        name, status, detail = run_fixture(fixture, rebuild)
        results.append((name, status))
        print(f"[{status:>9}] {name}")
        if status != "MATCH":
            print(detail)
    bad = [n for n, s in results
           if s not in ("MATCH", "SIM-SKIP", "KNOWN-DIFF", "XBUILD-FAIL")]
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
