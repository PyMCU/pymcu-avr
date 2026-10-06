# expect: refuse does not fit in 'x', which is declared uint8
# doc: https://docs.pymcu.org/language-reference/#type-casts
"""A literal a parameter's width cannot hold is refused, since PyMCU#af166930.

This probe used to be `# expect: divergence type-system.md:242`: the argument
was narrowed silently and the program printed 44 where CPython prints 300. That
divergence is the one the refusal removes -- the compiler now says so, names the
value the function would have received, and offers `uint8(300)` for a program
that really did mean 44.

The annotation is still a fixed storage width, so the divergence the citation
describes is alive everywhere a value reaches that width by some other route (an
assignment, a field, an arithmetic result); what changed is this one door, where
the value is a literal at the call site and the compiler can see it does not fit.
"""
from pymcu.types import uint8
def echo(x: uint8) -> uint8:
    return x
print(echo(300))
print("END")
