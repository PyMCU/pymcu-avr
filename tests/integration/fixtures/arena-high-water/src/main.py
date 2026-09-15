# PyMCU -- arena-high-water: the named global (pymcu.arena.arena_high_water) reflects
# the most bytes ever reserved -- readable directly from a .lst/.map or a debugger, and
# here through high_water(), the getter arena.py exposes because a plain module-level
# global cannot be read as `module.name` from outside the module today (a separate,
# pre-existing gap: module member access resolves functions, not data). Imported under a
# different alias than the driver's own auto-injected `_pymcu_arena` (which this file
# also triggers, for the two bytearray(n) calls below) to avoid a duplicate-import
# collision.
#
# n1 = 5, n2 = 7 (GPIOR0 reads 0 out of reset): high water should read 12 after both.
#
# Expected UART output:
#   12
#   done
import pymcu.arena as _arena_observe
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, uint16

n1: uint16 = uint16(GPIOR0.value) + 5
buf1: bytearray = bytearray(n1)
n2: uint16 = uint16(GPIOR0.value) + 7
buf2: bytearray = bytearray(n2)
print(_arena_observe.high_water())
print("done")
