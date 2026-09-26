# expect: match
# doc: docs/language/limitations.md:1061
# getattr(module, "name", default), the one compile-time form of getattr: a member that
# exists resolves to the member and one that does not folds to the default. The member is
# a register read at run time, so a fold that took the default instead prints 5 + 300.
import pymcu.chips.atmega328p as chip

r = getattr(chip, "GPIOR0", None)
print(r.value + 300)
print(getattr(chip, "NOPE", 5))
print("END")
