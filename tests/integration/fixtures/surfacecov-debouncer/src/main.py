# test201_surface_debouncer.py — whole-public-surface probe for adafruit_debouncer.
#
# Exercises every public member enumerated from the upstream source:
#   Debouncer: __init__ (ROValueIO and Callable union members), update()
#   (no-arg and new_state), interval get/set, value, rose, fell,
#   last_duration, current_duration.
#   Button: __init__ (**kwargs forwarding to super), update, pressed,
#   released, short_count, long_press.
#   adafruit_ticks: ticks_add, ticks_diff, ticks_less.
#
# Timing is deterministic: the oracle's supervisor.ticks_ms() reads a fake
# clock that time.sleep() advances; on AVR the same sleeps burn real
# milliseconds, so both sides see the same tick gaps between update() calls.

import time

import board
import digitalio

from adafruit_debouncer import Debouncer, Button
from adafruit_ticks import ticks_add, ticks_diff, ticks_less


# --- callable predicate path -------------------------------------------------
# A predicate whose answer the program controls through a mutable cell, so the
# Callable member of the union is exercised without any pin scripting.
cell = bytearray(1)


def pred() -> bool:
    return cell[0] != 0


# --- Debouncer over a DigitalInOut (ROValueIO member) ------------------------
dio = digitalio.DigitalInOut(board.D2)
dio.direction = digitalio.Direction.INPUT

d = Debouncer(dio, interval=0.010)   # pin reads low -> function() False
print("d.value", d.value)            # False
print("d.rose", d.rose)              # False
print("d.fell", d.fell)              # False
print("d.interval", d.interval)      # 0.01

d.update()                           # no-arg update: self.function() -> pin low
print("u0", d.value, d.rose, d.fell) # False False False

d.update(1)                          # new_state path: unstable toggle
print("u1", d.value, d.rose, d.fell) # False False False
time.sleep(0.02)
d.update(1)                          # 20ms > 10ms -> debounced True + changed
print("u2", d.value, d.rose, d.fell) # True True False
# duration members print as booleans: the exact value depends on real elapsed
# ms (UART prints burn sim time), but the sign is deterministic.
print("last_dur", d.last_duration > 0)    # True
print("cur_dur", d.current_duration >= 0) # True

d.update(1)                          # steady: changed clears
print("u3", d.value, d.rose, d.fell) # True False False

d.update(0)                          # unstable toggle the other way
time.sleep(0.02)
d.update(0)                          # debounced False + changed
print("u4", d.value, d.rose, d.fell) # False False True
print("last_dur", d.last_duration > 0) # True

d.interval = 0.05                    # interval setter
print("d.interval", d.interval)      # 0.05

# --- Debouncer over a predicate (Callable member) ----------------------------
p = Debouncer(pred, interval=0.01)   # pred() False at construction
print("p.value", p.value)            # False

cell[0] = 1
p.update()                           # self.function() -> pred() True: unstable
time.sleep(0.02)
p.update()                           # debounced True + changed
print("p rose", p.value, p.rose)     # True True

# --- Button -------------------------------------------------------------------
cell2 = bytearray(1)


def pred2() -> bool:
    return cell2[0] != 0


b = Button(pred2, short_duration_ms=200, long_duration_ms=500,
           value_when_pressed=True, interval=0.01)
print("b.value", b.value)            # False

cell2[0] = 1
b.update()                           # unstable only (0ms < 10ms)
print("b1", b.pressed, b.released, b.short_count, b.long_press)
time.sleep(0.02)
b.update()                           # rose -> pressed (vwp=True)
print("b2", b.pressed, b.released, b.short_count, b.long_press)

cell2[0] = 0
b.update()                           # unstable back
time.sleep(0.02)
b.update()                           # fell -> released
print("b3", b.pressed, b.released, b.short_count, b.long_press)

time.sleep(0.25)
b.update()                           # idle > 200ms -> short_count flushes to 1
print("b4", b.pressed, b.released, b.short_count, b.long_press)

cell2[0] = 1
b.update()                           # unstable
time.sleep(0.02)
b.update()                           # pressed again
print("b5", b.pressed, b.released, b.short_count, b.long_press)
time.sleep(0.6)
b.update()                           # held > 500ms -> long_press
print("b6", b.pressed, b.released, b.short_count, b.long_press)

# --- adafruit_ticks module functions ------------------------------------------
print("add", ticks_add(100, 5))      # 105
print("diff", ticks_diff(200, 100))  # 100
print("less", ticks_less(50, 100))   # True

print("=DONE=")
