# expect: refuse 'len' is a builtin function, named here without being called
# doc: https://github.com/PyMCU/PyMCU/issues
# A builtin function named without a call compiled to the integer 0. CPython prints the
# function object; there is none on the target, so the name is refused and the message says
# why. An undefined name is refused differently ("name 'foo' is not defined").
print(len)
print("END")
