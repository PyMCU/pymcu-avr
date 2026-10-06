# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import inline
@inline
def risky(x):
    if x > 5:
        raise ValueError("too high")
    return x
try:
    print(risky(9))
except ValueError:
    print(-1)
print("END")
