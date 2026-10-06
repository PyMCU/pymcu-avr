# expect: match
# doc: https://docs.pymcu.org/limitations/
# hex(-1) used to print "0xffffffff" (the raw 32-bit two's complement pattern formatted as
# unsigned hex) instead of CPython's "-0x1": a silent wrong answer, not a refusal, for every
# compile-time NEGATIVE argument to hex()/bin()/oct(). The sign now spells before the base
# prefix, matching CPython.
print(hex(-1))
print(hex(-255))
print(bin(-2))
print(oct(-8))
print(hex(255))
print("END")
