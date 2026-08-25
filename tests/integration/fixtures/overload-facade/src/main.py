# PyMCU -- overload-facade: an overloaded constructor reached through a re-exporting facade.
#
# The facade half of PyMCU#75, plus the overload-selection half it was hiding.
#
# Two separate failures, both in this one program:
#
#   1. Once a name is overloaded its bare key is vacated so that suffix resolution can
#      work, and the re-export copied the suffixed keys to the facade prefix but not the
#      record that the name was overloaded. The constructor was then found under neither,
#      and the build stopped with "'Low' is not exported by mid. Did you mean 'Low'?",
#      offering as the near miss the name it had just refused.
#
#   2. With that fixed the build succeeded and picked the WRONG overload, because an
#      argument that is a FIELD was typed by inference alone, which has no string to
#      report. A const[str] field bound to the numeric overload.
#
# k is seeded from GPIOR0 so the tag cannot be folded to a constant: the overload is
# chosen at compile time, the value it produces is not.
#
# Expected UART output with GPIOR0 = 0:
#   20
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8
from pymcu.hal.console import print
from top import Wrapper


def main():
    k: uint8 = GPIOR0.value
    w = Wrapper(k)
    print(w.tag)
    print("done")
