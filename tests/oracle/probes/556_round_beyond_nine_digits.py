# expect: match
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# round(x, n) advertised n in -15..15, but the decimal-fraction accumulator was
# uint32 and overflowed past 9 digits: round(0.5, 10) printed 0.07050327. The
# fraction is now rebuilt digit-by-digit in float32 (exact to the last digit for
# every n in range), matching CPython.
print(round(0.5, 10))
print(round(0.5, 15))
print(round(0.15625, 12))
print(round(99999.5, 10))
print(round(0.75, 13))
print(round(-2.5, 12))
print("END")
