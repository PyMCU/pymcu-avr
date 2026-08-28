# PyMCU -- dict-one-char-key: a one-character key held in a name finds its entry.
#
# Regression for PyMCU#215. A one-character string has TWO encodings: as a literal it folds to
# its character code, and through a name it resolves to its interned id. EmitDictLookup compared
# the numbers, so `k = "a"` did not match the entry `"a"`. `d[k]` raised KeyError naming 256, a
# number the program never wrote, and `d.get(k, 1)` silently returned the default. A build with
# no diagnostic and the wrong number in the firmware.
#
# THE `d[k]` SPELLING IS NOT HERE, and that is deliberate. It is the same site, differing only
# in whether a default was passed, but unfixed it does not return a wrong value: it FAILS THE
# BUILD with "KeyError: 257". Put in this program it stops the build before any of the silent
# rows can be observed, and then the fixture can only ever demonstrate the loud half. It lives
# in dict-one-char-key-index so that this program measures the SILENT wrong values, which are
# the half the issue says matters.
#
# Every value is distinct so no two rows can be confused, and every row is a DIFFERENT key so a
# lookup that ignored the key entirely would show up rather than pass.
#
#   row  written                                    unfixed   correct
#   a    d = {"a":70,"b":7} ; k="a" ; d.get(k,1)      1         70
#   b    same dict          ; k="b" ; d.get(k,1)      1          7
#   d    {"a":70,"bb":9}    ; k="a" ; d.get(k,1)      1         70   <- per key, not per dict
#   e    {"a":70,"bb":9}    ; k="bb"; d.get(k,1)      9          9   <- was already right
#   f    same dict          ; k="z" ; d.get(k,1)      1          1   <- absent, default IS right
#
# Row f is the one that looks fine either way: the key really is absent, so the default is the
# correct answer and the broken mechanism produced it too. It is kept because a fix that made
# every lookup hit would break it, and dropped rows are how that goes unnoticed.
#
# Row e is the control for the reported bound: a multi-character key was never affected, because
# both encodings are the same interned id for it.
#
# No seed: the keys and the dict ARE literals, which is the construct under test. There is
# nothing for a folder to hide, because the defect is in what the fold compares.
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.types import asm, uint8


def main():
    d = {"a": 70, "b": 7}
    m = {"a": 70, "bb": 9}

    ka = "a"
    kb = "b"
    kz = "z"
    kbb = "bb"

    GPIOR0.value = uint8(d.get(ka, 1))
    GPIOR1.value = uint8(d.get(kb, 1))
    GPIOR2.value = uint8(m.get(ka, 1)) + uint8(m.get(kbb, 1)) + uint8(m.get(kz, 1))

    asm("BREAK")
    while True:
        pass


main()
