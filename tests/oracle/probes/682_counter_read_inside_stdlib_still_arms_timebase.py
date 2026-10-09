# expect: match
# doc: docs/rfcs/0014-by-type-not-by-name.md p17
# asyncio.ticks() answers the microsecond counter, and the call to micros() it
# makes lives INSIDE the stdlib module -- the callsite is not the program's
# file. A token gate that asked "is this call written in the program" left the
# counter never armed: ticks() would return 0 forever on hardware (the same
# freeze Codex's review pinned on keypad.EventQueue.get_into, which reads
# ticks_ms() the same way). The resolved callee, not the file the call sits
# in, is what matters: the oracle sees it as a counter that actually moves.
# delay_ms(2) first so the armed counter is unambiguously past zero before the
# read -- this is the timer's pace, not a timing coincidence.
import asyncio
from pymcu.time import delay_ms

delay_ms(2)
v = asyncio.ticks()
if v:
    print(1)
else:
    print(0)
print("END")
