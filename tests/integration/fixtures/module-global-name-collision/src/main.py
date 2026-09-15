# A user's own import does not change how a layer function's parameters bind (PyMCU#381).
#
# `alarm.py` names its module-level singleton `time`, to give CircuitPython's
# `alarm.time.TimeAlarm` spelling. A user's plain `import time` against that name was
# enough to file `alarm` itself as an instance, so the dotted call
# `alarm.sleep_until_alarms(ta)` was given the receiver offset a method gets and bound its
# first argument to the SECOND parameter. `alarm0` was then bound to nothing, and the first
# read of it inside the library was reported as a name that is not defined -- naming a
# parameter written in the signature two lines above, in a file the user never opened.
#
# The callee has no `self`, which is the one fact that settles the offset whatever put it
# there. Deleting `import time` made the identical library compile, which is what said the
# defect was the collision and not the library.
#
# Expected UART output, which is what CPython prints for the same program:
#   0
#   done
import time
import board
import alarm

time_alarm = alarm.time.TimeAlarm(monotonic_time=0.0)
pin_alarm = alarm.pin.PinAlarm(pin=board.D2, value=False, pull=True)


def main() -> None:
    wake = alarm.sleep_until_alarms(time_alarm, pin_alarm)
    print(wake)
    print("done")
    while True:
        pass
