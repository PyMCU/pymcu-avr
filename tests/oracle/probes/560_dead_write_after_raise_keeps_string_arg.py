# expect: refuse reads the raise's integer argument
# doc: https://docs.pymcu.org/limitations/#exception-handling
# A write that cannot reach the raise (unreachable after it) used to retype the
# raise's argument: `code` bound to "oops" was classified by the LAST textual
# write in the function, `code: uint8 = 5`, so the raise stored a dead word and
# e.args[0] answered 0. The binding scan now stops at the raise and prunes the
# dead tail, so the raise carries the string -- and a VALUE read of args[0] is
# refused for what it is: a string message has no integer to answer.
from pymcu.types import uint8

def fail():
    code = "oops"
    raise OSError(code)
    code: uint8 = 5

try:
    fail()
except OSError as e:
    value = e.args[0]
    print(value)
print("END")
