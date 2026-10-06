# expect: match
# doc: https://docs.pymcu.org/limitations/
# oct() used to be entirely unimplemented (the generic "builtin PyMCU does not provide"
# refusal). The P2 AVR gaps bundle added it alongside hex()/bin(): a compile-time constant
# interns its spelling, and it takes a run-time argument too (see 498/499).
print(oct(8))
print(oct(0))
print(oct(-8))
print("END")
