# CircuitPython alarm module integration test fixture
#
# Exercises the alarm sleep entry points with a TimeAlarm on real AVR:
#   - alarm.light_sleep_until_alarms()          (light sleep)
#   - alarm.exit_and_deep_sleep_until_alarms()  (deep-sleep entry)
#   - alarm.sleep_until_alarms()                (the DIRECT call, PyMCU#271)
#
# Each TimeAlarm wakes ~50 ms in the future; sleep_until_alarms() blocks in a
# delay until the absolute monotonic_time is reached, then returns. Markers
# bracket each sleep so the simulator confirms both calls run and return.
#
# Expected UART output: "ABCDE"
#
import board
import busio
import alarm
import time


def main():
    uart = busio.UART(board.TX, board.RX, baudrate=9600)
    uart.write(b"A")

    a1 = alarm.time.TimeAlarm(monotonic_time=time.monotonic() + 0.05)
    alarm.light_sleep_until_alarms(a1)
    uart.write(b"B")

    a2 = alarm.time.TimeAlarm(monotonic_time=time.monotonic() + 0.05)
    alarm.exit_and_deep_sleep_until_alarms(a2)
    uart.write(b"C")

    # The DIRECT call, which the two above reach only through one more @inline
    # forward. It is the case that used to fail (PyMCU#271) while its own two
    # wrappers compiled, so both directions belong in one fixture: if a change
    # ever fixes the direct call by routing the wrappers down the broken path,
    # the markers below stop appearing in order.
    a3 = alarm.time.TimeAlarm(monotonic_time=time.monotonic() + 0.05)
    alarm.sleep_until_alarms(a3)
    uart.write(b"D")

    uart.write(b"E")
    while True:
        pass
