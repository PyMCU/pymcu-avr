# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""Fixed at #378: a conditional expression that yields a str writes its text.

It used to write the interned id of the arm it selected, as a decimal number,
both written straight into the print and bound to a name first. The same two
texts chosen by an if/else STATEMENT always printed correctly, which is the
control this probe keeps beside the expression.
"""
from pymcu.chips.atmega328p import GPIOR0

k = GPIOR0.value
print("mono" if k == 0 else "none")
label = "mono" if k == 0 else "none"
print(label)
if k == 0:
    stmt = "mono"
else:
    stmt = "none"
print(stmt)
print("GRB" if 3 == 3 else "GRBW")
print("END")
