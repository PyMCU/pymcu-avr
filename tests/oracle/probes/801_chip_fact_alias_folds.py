# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `from pymcu.chips import __CHIP__ as C` binds the fact under the alias: the
# fold follows the binding, not the spelling.
from pymcu.chips import __CHIP__ as C

if C.name == "atmega328p":
    print("aliased")
print("END")
