# expect: match
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# hex() of a call that returns int16 -1 printed 0xffffffff: the signedness check
# looked at the argument's SYNTAX and had no case for a call, so it fell to
# unsigned. The check now reads the evaluated value's type -- the same fix
# covers bin() and oct().
from pymcu.types import int16

def minus_one() -> int16:
    return -1

def minus_255() -> int16:
    return -255

print(hex(minus_one()))
print(bin(minus_one()))
print(oct(minus_255()))
print("END")
