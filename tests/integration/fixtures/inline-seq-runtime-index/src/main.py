# PyMCU -- inline-seq-runtime-index: `seq[i]` with a run-time i, where `seq`
# reached the subscript through stacked @inline parameter hops (#258).
#
# pulseio.PulseOut.send(pulses, n) hands the caller's `signal = [...]` to
# _PulseTrain.send(pulses, n), and the innermost loop subscripts `pulses[i]`
# with i decided at run time. The sequence has no storage of its own -- it was
# a compile-time constant bound by name -- so the read must go to a
# materialised flash table. Before the fix the subscript fell through to the
# register-bit path and refused with "Bit index must be constant".
#
# The sibling shape from the same issue -- `for b in buf` over a DECLARED
# module array handed to an @inline -- is covered too: the alias resolves to
# `main.buf` while the array is registered bare as `buf`, and the for-in base
# needs the same storage-key normalisation.
#
# Covered shapes:
#   pulses[i]   run-time index, undeclared list through two @inline hops
#               (the pulseio send chain)
#   for b in d  direct iteration, declared uint8[N] through one @inline hop
#   d[i]        run-time read of a declared module array (no inline)
#
# Expected UART output:
#   560
#   1690
#   100
#   30
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART
from pymcu.types import uint8, inline

signal = [9000, 4500, 560, 560, 560, 1690, 560, 560]
delays: uint8[4] = [10, 20, 30, 40]


class Train:
    @inline
    def send(self, pulses, n: uint8):
        i: uint8 = n
        while i < n + 1:
            print(pulses[i])
            i = i + 1


class Out:
    def __init__(self):
        self._train = Train()

    @inline
    def send(self, pulses, count: uint8):
        self._train.send(pulses, count)


@inline
def total(buf, n: uint8) -> uint8:
    s: uint8 = 0
    for b in buf:
        s = s + b
    return s


uart = UART(9600)
out = Out()

# The pulseio shape: run-time index inside the innermost of two @inline hops.
i: uint8 = 2
out.send(signal, i)
i = 5
out.send(signal, i)

print(total(delays, 4))

i = 1
print(delays[i + 1])

print("done")

while True:
    pass
