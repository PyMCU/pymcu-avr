# test201_surface_neopixel.py -- whole-surface probe for neopixel.NeoPixel.
# Members exercised (surface.tsv): RGB/GRB/BGR/RGBW/GRBW module constants,
# NeoPixel.__init__ (pin, n, bpp, brightness, auto_write, pixel_order),
# NeoPixel.n (property), NeoPixel.write(), NeoPixel.deinit(), __enter__/__exit__
# via a with block. The inherited PixelBuf protocol (len, getitem, setitem,
# fill, show, brightness) is the only way to reach those members, so it is
# exercised along the way.
#
# Two strips: an 8-pixel GRB strip on D6 with auto_write, and a 4-pixel GRBW
# strip on D7 with auto_write off inside a with block. neopixel_write is a
# no-op on the oracle and bit-bangs a data pin on the sim; either way the
# observable surface is what print() says.
import board

import neopixel

print(neopixel.RGB)
print(neopixel.GRB)
print(neopixel.BGR)
print(neopixel.RGBW)
print(neopixel.GRBW)

px = neopixel.NeoPixel(board.D6, 8, brightness=0.5, auto_write=True)
print(px.n)
print(len(px))

px[0] = (200, 100, 50)
v = px[0]
print(v[0])
print(v[1])
print(v[2])

px[1] = 0x102030
w = px[1]
print(w[0])
print(w[1])
print(w[2])

px.fill((0, 0, 40))
v2 = px[7]
print(v2[0])
print(v2[1])
print(v2[2])

px.show()
px.write()

px.brightness = 0.25
print(px.brightness)
v3 = px[0]
print(v3[0])
print(v3[1])
print(v3[2])

with neopixel.NeoPixel(board.D7, 4, bpp=4, pixel_order=neopixel.GRBW, auto_write=False) as p2:
    p2[0] = (10, 20, 30, 40)
    print(p2.n)
    print(len(p2))
    p2.show()

px.deinit()

print("=DONE=")
