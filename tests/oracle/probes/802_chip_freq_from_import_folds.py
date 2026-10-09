# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `from pymcu.chips import __FREQ__` binds the clock the project asked for.
from pymcu.chips import __FREQ__

if __FREQ__ == 16000000:
    print("freq")
if __FREQ__ > 1000000:
    print("fast")
print("END")
