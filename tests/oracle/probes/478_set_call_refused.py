# expect: refuse set() is a Python builtin that PyMCU does not provide: a growable set needs a heap
# doc: src/compiler/IR/IRGenerator/Call.cs:6958
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

st = set()
st.add(s)
print(len(st))
print("END")
