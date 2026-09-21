# compat-cp-introspection: PyMCU#266 / RFC 0007.
#
# Under stdlib = ["circuitpython"] the driver passes --stdlib circuitpython to
# pymcuc, and `if` conditions on sys.implementation.name, .version[i],
# sys.platform and uname() fold at compile time from the per-board table --
# the way __CHIP__ conditions already did -- so a dead branch is gone from the
# image, not merely untaken.
#
# WHAT DISCRIMINATES:
#   IMPL-CP    -- sys.implementation.name == "circuitpython" is true here
#   VER-GE7    -- sys.implementation.version[0] >= 7 on the claimed API surface
#   PLAT-CHIP  -- sys.platform is the chip name on a part with no upstream port
#   UNAME-CHIP -- uname().sysname answers the chip name, not "rp2040"
#   NO-LINUX   -- "Linux" not in uname(), the adafruit_dht guard
#   dead_branch_marker: absent from firmware.asm -- its only call site was in
#   the folded-away `not ... == "circuitpython"` branch.
import sys
from os import uname


def dead_branch_marker():
    print("unreachable")


def main():
    # adafruit_requests's guard shape (PyMCU#266): dead under circuitpython.
    if not sys.implementation.name == "circuitpython":
        dead_branch_marker()

    if sys.implementation.name == "circuitpython":
        print("IMPL-CP")
    else:
        print("IMPL-OTHER")

    if sys.implementation.version[0] >= 7:
        print("VER-GE7")
    else:
        print("VER-LT7")

    if sys.platform == "atmega328p":
        print("PLAT-CHIP")
    else:
        print("PLAT-OTHER")

    if uname().sysname == "atmega328p":
        print("UNAME-CHIP")
    else:
        print("UNAME-OTHER")

    if "Linux" not in uname():
        print("NO-LINUX")
    else:
        print("HAS-LINUX")

    print("END")
