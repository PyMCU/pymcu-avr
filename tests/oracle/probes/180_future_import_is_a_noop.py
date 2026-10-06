# expect: match
# doc: https://docs.pymcu.org/limitations/
from __future__ import annotations

def f(x: int) -> int:
    return x + 1

print(f(3))
print("END")
