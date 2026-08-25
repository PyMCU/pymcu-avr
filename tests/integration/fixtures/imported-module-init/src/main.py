# PyMCU -- imported-module-init: an imported module's module level actually runs.
#
# Regression for PyMCU#129. Only the ENTRY file's module level was executed, so anything an
# imported module set up at its top level arrived as zero: a plain `n: uint16 = 7`, and an
# object built as `c = C(5)`. The storage and the later writes were real, so a counter in an
# imported module counted 0, 1, 2 instead of 5, 6, 7. Only the initial value was lost, which
# is why it compiled, ran, and was wrong by a constant.
#
# Each imported module's top level is now compiled as a synthesized __module_init under that
# module's own prefix, called before anything the entry file does, because an import runs
# before the file that imports it.
#
# Expected UART output:
#   7
#   5
#   6
#   7
#   done
from pymcu.hal.console import print
from counter import bump, get, plain


def main():
    print(plain())
    print(get())
    bump()
    print(get())
    bump()
    print(get())
    print("done")
    while True:
        pass


main()
