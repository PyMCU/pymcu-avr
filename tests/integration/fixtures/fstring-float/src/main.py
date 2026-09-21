# PyMCU -- fstring-float: f"{v:.Nf}" / f"{v:W.Nf}" on float values.
#
# The digits come from the EXACT decimal expansion of the float32 (a dyadic
# m * 2^-e), so a .5 boundary is a real tie and rounds half-to-even -- the same
# string CPython produces for the float64 holding that exact float32. The
# expected UART output below is CPython's, computed that way.
#
# Expected UART output:
#   FFMT
#   a1=23.5
#   a2=23.46
#   a6=23.455999
#   aW=   23.4560
#   h0=0
#   i0=2
#   j0=2
#   k0=4
#   e2=0.12
#   g2=0.38
#   f2=2.67
#   m2=1.00
#   pd=3.141593
#   p3=3.142
#   b2=12345.68
#   t4=0.0010
#   n1=-12.3
#   qW=  -1.500
#   qZ=-0001.50
#   z0=-0
#   aS=  23.456
#   aZ=00023.46
#   c1=1000.0
#   r1=255.0
#   buf=23.46|-12.3
#   done
from pymcu.hal.console import print
from pymcu.types import uint8


def main():
    print("FFMT")
    a: float = 23.456
    print(f"a1={a:.1f}")
    print(f"a2={a:.2f}")
    print(f"a6={a:.6f}")
    print(f"aW={a:10.4f}")

    h: float = 0.5
    print(f"h0={h:.0f}")      # exact tie -> even
    i: float = 1.5
    print(f"i0={i:.0f}")
    j: float = 2.5
    print(f"j0={j:.0f}")      # exact tie -> even, not 3
    k: float = 3.5
    print(f"k0={k:.0f}")
    e: float = 0.125
    print(f"e2={e:.2f}")      # exact tie -> even
    g: float = 0.375
    print(f"g2={g:.2f}")
    f: float = 2.675
    print(f"f2={f:.2f}")      # float32 is 2.67499995231... -> 2.67
    m: float = 1.005
    print(f"m2={m:.2f}")      # float32 is 1.00499999523... -> 1.00

    p: float = 3.14159265
    print(f"pd={p:f}")        # default precision 6
    print(f"p3={p:.3f}")
    big: float = 12345.678
    print(f"b2={big:.2f}")
    tiny: float = 0.0009765625
    print(f"t4={tiny:.4f}")
    n: float = -12.345
    print(f"n1={n:.1f}")
    q: float = -1.5
    print(f"qW={q:8.3f}")
    print(f"qZ={q:08.2f}")    # zero-pad keeps the sign in front
    z: float = -0.5
    print(f"z0={z:.0f}")      # negative zero, like CPython's "-0"
    print(f"aS={a:8.3f}")
    print(f"aZ={a:08.2f}")
    c: float = 999.999
    print(f"c1={c:.1f}")      # carry into the integer part

    r: uint8 = 255
    print(f"r1={r:.1f}")      # int operand under an f spec, like CPython

    s = f"buf={a:.2f}|{n:.1f}"
    print(s)

    print("done")
