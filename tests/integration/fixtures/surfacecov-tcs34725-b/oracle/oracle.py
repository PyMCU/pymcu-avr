#!/usr/bin/env python3
# CPython oracle for this fixture: runs src/main.py under stock CPython with
# the fake CircuitPython modules in oracle_common.py (shared by every
# surfacecov fixture) and prints what the program prints, formatted the way
# PyMCU's UART writers format it. fixture.json next to this file decides the
# bus answers, exactly like the sim side reads it.
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(_HERE, "..", "..", "_surfacecov_oracle")))
import oracle_common

oracle_common.run_here(os.path.abspath(__file__))
