# expect: match
# doc: docs/language/limitations.md:802
"""Fixed at #438: a condition compares two strings by their text.

`x == "abc"` on a name bound to a multi-character string used to be folded
always-false and the `yes` branch was deleted from the image: not a wrong value
printed, code removed. The one-character spelling, whose id IS its character
code and so fits the name's one-byte slot, answered correctly the whole time.
"""
x = "abc"
if x == "abc":
    print("yes")
else:
    print("no")
print("END")
