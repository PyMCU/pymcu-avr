# A raise reaches the handler written for it, whatever a call before it left in the
# T flag (PyMCU#384).
#
# The exception model signals through the T flag, and a `try` body reads it with a BRTS
# after every call it contains, because the callee's CanFail is not known when the body
# is lowered. T is not part of the AVR calling convention, and libgcc's soft-float
# routines carry a sign bit through BST/BLD and return with it in whatever state their
# last operation left.
#
# So printing a float -- anywhere earlier, any value -- armed the guard that followed
# the `time.sleep()` inside the method, and the raise below it was never reached: the
# dispatcher ran with a stale code in the error register, matched no handler, and the
# program halted with E:RuntimeError while the handler sat three lines away.
#
# All three ingredients are here, because removing any one of them hid it: a float
# printed before the try, a call inside the raising method just before the raise, and
# the raise inside an INSTANCE METHOD, which is inlined into the try body so the guard
# and the raise end up in the same function.
#
# Expected UART output, which is what CPython prints for the same program:
#   0.1
#   Retrying!
#   END
import time


class Sonar:
    def __init__(self) -> None:
        pass

    def _dist_two_wire(self) -> None:
        time.sleep(0.00001)
        raise RuntimeError("Timed out")


sonar = Sonar()


def main() -> None:
    print(0.1)
    try:
        sonar._dist_two_wire()
    except RuntimeError:
        print("Retrying!")
    print("END")
    while True:
        pass
