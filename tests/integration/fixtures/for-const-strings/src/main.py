# for-const-strings: `for` over a constant list or pair-list of strings binds
# the loop variable to each TEXT. The element folder hands the unroller the
# interned id of a string literal (256, 257, ...), and the bug printed the id
# where the name was meant (oracle probes 009/010).
#
# Expected UART output:
#   PD2
#   PD3
#   2
#   D2
#   3
#   D3
#   END
from pymcu.hal.console import print

for name in ["PD2", "PD3"]:
    print(name)
for pin, name in [(2, "D2"), (3, "D3")]:
    print(pin)
    print(name)
print("END")
