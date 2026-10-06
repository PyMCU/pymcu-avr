# expect: refuse undefined reference
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.ffi import extern
from pymcu.types import uint8
@extern("oracle_missing_symbol")
def missing() -> uint8:
    pass
print(missing())
print("END")
