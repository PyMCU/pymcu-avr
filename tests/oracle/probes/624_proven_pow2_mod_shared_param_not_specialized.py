# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Codex review of PyMCU golperf's proven-power-of-two mod rewrite (case 2): a
# real (non-@inline) function's body is generated exactly once and must be
# correct for every call site sharing its parameter slots. An argument copy
# into the callee's parameter must never leave a "proven" range for the
# PARAMETER itself behind: rem(5, 32) then rem(5, 4) share one `x % n` body,
# and the second call's divisor must not specialize the first call's answer.
from pymcu.types import int16, uint16


def rem(x: int16, n: uint16) -> int16:
    return x % n


print(rem(5, 32))
print(rem(5, 4))
print("END")
