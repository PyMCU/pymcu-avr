# import-alias-constant-check: pymcu-circuitpython's busio.py
# `_i2c_check(rc)` shape end to end --
#   if rc == _I2C_ABRT_NODEV: raise A()
#   elif rc != _I2C_OK: raise B()
# (no else: the implicit third path is "do nothing"). ResolveBindingLadder's
# "imported alias" rung re-keyed a MUTABLE global through the alias's
# original name correctly, but only ever checked mutableGlobals for it -- a
# plain module-level constant like OK_VAL/OTHER_VAL in codes.py is never a
# mutable global, so the renamed names below resolved to an uninitialized
# local instead of the literal, and every branch ran for every rc.
#
# WHAT DISCRIMINATES: the three outcomes below must each print their OWN
# case ("A", "B", "C") and nothing else, for exactly the rc that triggers it.
from codes import OK_VAL as _OK, OTHER_VAL as _OTHER
from pymcu.types import uint8


def check(rc: uint8):
    if rc == _OTHER:
        print("A")
    elif rc != _OK:
        print("B")
    else:
        print("C")


def main():
    # Literals, not _OTHER/_OK: the call argument must not go through the
    # same alias lookup the comparison is being tested for, or a broken
    # argument and a broken comparison could cancel out and look correct.
    # 20 is OTHER_VAL and 10 is OK_VAL on AVR (codes.py).
    check(20)
    check(99)
    check(10)
    print("END")


main()

while True:
    pass
