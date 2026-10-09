# expect: refuse uname
# doc: https://docs.pymcu.org/limitations/
# A bare `uname()` the file never imported is the program's own (missing)
# function, not the introspection call.
print(uname().machine)
