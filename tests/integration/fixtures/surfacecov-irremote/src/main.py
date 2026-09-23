# Whole-surface probe for adafruit_irremote: every public member exercised.
# Under PyMCU this fixture is build-refused -- `import adafruit_irremote` lowers
# every class member and NonblockingGenericDecode.read() is a generator, which
# is a documented SyntaxError ("a generator has to be a module-level function").
# The CPython oracle still runs the full program so the report records what
# each member should do.

import board
import pulseio
from adafruit_irremote import (
    IRDecodeException,
    IRNECRepeatException,
    FailedToDecode,
    IRMessage,
    UnparseableIRMessage,
    NECRepeatIRMessage,
    bin_data,
    decode_bits,
    GenericDecode,
    NonblockingGenericDecode,
    GenericTransmit,
)

pin = pulseio.PulseIn(board.D2, maxlen=120, idle_state=True)
dec = GenericDecode()

# read_pulses + decode_bits: scripted NEC frame (0x10,0xEF,0x20,0xDF on wire).
pulses = dec.read_pulses(pin)
print(len(pulses))
code = dec.decode_bits(pulses)
print(code[0], code[1], code[2], code[3])

# Module-level decode_bits returns the IRMessage namedtuple.
msg = decode_bits(pulses)
print(msg.code[0], msg.code[3], len(msg.pulses))

# Module-level bin_data on the same burst.
bins = bin_data(pulses)
print(len(bins))
for b in bins:
    print(b[0], b[1])

# NEC repeat frame -> IRNECRepeatException through GenericDecode.decode_bits.
pin.resume()
pulses = dec.read_pulses(pin)
try:
    dec.decode_bits(pulses)
    print("no repeat")
except IRNECRepeatException:
    print("nec-repeat")

# Too-short burst -> FailedToDecode -> IRDecodeException.
pin.resume()
pulses = dec.read_pulses(pin)
try:
    dec.decode_bits(pulses)
    print("no fail")
except IRDecodeException:
    print("decode-fail")

# The namedtuples themselves.
m = IRMessage((10, 20, 30), code=(170, 85))
print(m.pulses[0], m.pulses[2], m.code[0], m.code[1])
u = UnparseableIRMessage((5, 6), reason="Too short")
print(u.pulses[1], u.reason)
r = NECRepeatIRMessage((9000, 2250, 560))
print(len(r.pulses), r.pulses[1])

# NonblockingGenericDecode: a partial frame (no end-of-message gap) stashes;
# the gap pulse on the next read() finishes it and yields the IRMessage.
nb = NonblockingGenericDecode(pin, max_pulse=10000)
pin.resume()
for res in nb.read():
    print("unexpected")
pin.resume()
for res in nb.read():
    print(res.code[0], res.code[3])
pin.resume()
for res in nb.read():
    print(res.reason)
pin.resume()
for res in nb.read():
    print("unexpected")

# GenericTransmit: NEC timing onto the 38 kHz carrier pin.
tx = GenericTransmit(header=[9000, 4500], one=[560, 1690], zero=[560, 560], trail=560)
po = pulseio.PulseOut(board.D3, frequency=38000, duty_cycle=32768)
tx.transmit(po, bytearray([0x10, 0xEF]), nbits=16)
tx.transmit(po, bytearray([0x20]), repeat=1, delay=0.02)
po.deinit()
print("sent")

print("=DONE=")
