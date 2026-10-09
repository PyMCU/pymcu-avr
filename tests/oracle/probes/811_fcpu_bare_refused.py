# expect: refuse from pymcu.chips import F_CPU
# doc: https://docs.pymcu.org/limitations/
# `F_CPU` without the import is not the ambient constant C firmware makes it --
# the error names the binding.
x = F_CPU
