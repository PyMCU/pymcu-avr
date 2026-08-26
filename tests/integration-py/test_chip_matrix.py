"""
Every chip in the catalog must assemble, link and boot with its own stack pointer.

Two bugs hid behind a corpus that only ever built for the ATmega328P. The stack
pointer was initialised to the 328P's RAMEND (0x08FF) on every ATmega, so an
ATmega48 pushed 1.5 KB past the end of its 512-byte SRAM; and the ATmega48/88
parts (avr4) and every ATtiny (avr25) have no JMP/CALL, so any program with a
real function call was rejected by the assembler -- "illegal opcode ... for mcu
atmega48", naming an opcode one entry off in avr-as's own table.

The programs below are deliberately HAL-free (no Pin, no UART): what is under
test is the chip's memory layout and instruction set, not its peripheral
coverage.

Run with:
    pytest tests/integration-py/test_chip_matrix.py -v
"""

from __future__ import annotations

import re

import avr8sharp
import pytest

import pymcu_compiler

CALLS_A_FUNCTION = """\
from pymcu.types import uint8


def step(n: uint8) -> uint8:
    return n + 1


def main():
    x: uint8 = 0
    while True:
        x = step(x)
"""

# No call, so the stack stays exactly where the startup code put it.
SPINS = """\
from pymcu.types import uint8


def main():
    x: uint8 = 0
    while True:
        x = x + 1
"""

TIMER1_ISR = """\
from pymcu.types import uint8
from pymcu.hal.timer import Timer

tick: uint8 = 0


def on_overflow():
    global tick
    tick = 1


def main():
    timer = Timer(1, 256)
    timer.irq(on_overflow)
    while True:
        if tick == 1:
            tick = 0
"""

# Byte address of the Timer1 overflow slot, as avr-gcc places __vector_13: the
# table strides by one word where the core has no JMP and by two where it has.
TIMER1_OVF_SLOT = {
    "atmega48": 0x1A, "atmega48p": 0x1A, "atmega88": 0x1A, "atmega88p": 0x1A,
    "atmega168": 0x34, "atmega168p": 0x34, "atmega328": 0x34, "atmega328p": 0x34,
}


def flash_image(hex_text: str) -> bytes:
    mem, base = bytearray(), 0
    for line in hex_text.splitlines():
        if not line.startswith(":"):
            continue
        count, addr, kind = int(line[1:3], 16), int(line[3:7], 16), int(line[7:9], 16)
        data = bytes.fromhex(line[9:9 + 2 * count])
        if kind == 0:
            at = base + addr
            if len(mem) < at + count:
                mem.extend(b"\xff" * (at + count - len(mem)))
            mem[at:at + count] = data
        elif kind == 4:
            base = int.from_bytes(data, "big") << 16
        elif kind == 2:
            base = int.from_bytes(data, "big") << 4
    return bytes(mem)


def jump_target(mem: bytes, at: int) -> int | None:
    """Byte address an RJMP/JMP at *at* transfers control to."""
    word = mem[at] | (mem[at + 1] << 8)
    if word & 0xF000 == 0xC000:
        offset = word & 0x0FFF
        if offset >= 0x800:
            offset -= 0x1000
        return at + 2 + 2 * offset
    if word & 0xFE0E == 0x940C:
        return 2 * (mem[at + 2] | (mem[at + 3] << 8))
    return None

# The SRAM check used to reserve a flat 64 bytes for the call stack, which is the
# whole SRAM of an ATtiny13, so no program with static data compiled for it at all.
# The reservation is min(64, SRAM // 2) now, so the two 64-byte parts keep 32 bytes
# for the stack and every larger part keeps the same 64 it always had. The set is
# empty rather than deleted: it is where the next part that cannot fit goes.
NO_ROOM_FOR_STATICS: set[str] = set()


def catalog() -> list[tuple[str, int]]:
    """(chip, RAMEND) for every AVR chip PyMCU ships, read from its chip file."""
    chips_dir = pymcu_compiler.REPO_ROOT.parent / "pymcu" / "lib" / "src" / "pymcu" / "chips"
    assert chips_dir.is_dir(), f"chip catalog not found at {chips_dir}"
    out = []
    for path in sorted(chips_dir.glob("*.py")):
        text = path.read_text()
        if not re.search(r'device_info\([^)]*arch\s*=\s*"avr"', text):
            continue
        start = re.search(r"^RAM_START\s*=\s*(0x[0-9A-Fa-f]+|\d+)", text, re.M)
        size = re.search(r"^RAM_SIZE\s*=\s*(0x[0-9A-Fa-f]+|\d+)", text, re.M)
        assert start and size, f"{path.name} declares no RAM_START/RAM_SIZE"
        out.append((path.stem, int(start.group(1), 0) + int(size.group(1), 0) - 1))
    assert out, "the AVR chip catalog came back empty"
    return out


def cases() -> list:
    return [
        pytest.param(chip, ramend, id=chip,
                     marks=pytest.mark.xfail(strict=True, reason="call-stack floor")
                     if chip in NO_ROOM_FOR_STATICS else ())
        for chip, ramend in catalog()
    ]


@pytest.mark.parametrize("chip,ramend", cases())
def test_a_program_with_a_call_assembles(chip, ramend):
    assert pymcu_compiler.build_source(CALLS_A_FUNCTION, target=chip, frequency=8_000_000)


@pytest.mark.parametrize("chip,slot", sorted(TIMER1_OVF_SLOT.items()))
def test_the_isr_lands_on_its_hardware_vector_slot(chip, slot):
    mem = flash_image(pymcu_compiler.build_source(TIMER1_ISR, target=chip,
                                                  frequency=16_000_000))
    stride = 4 if slot == 0x34 else 2

    targets = {at: jump_target(mem, at) for at in range(stride, 26 * stride, stride)}
    unused = max(set(targets.values()), key=list(targets.values()).count)

    assert [at for at, t in targets.items() if t != unused] == [slot]


@pytest.mark.parametrize("chip,ramend", cases())
def test_stack_pointer_is_the_chips_own_ramend(chip, ramend):
    firmware = pymcu_compiler.build_source(SPINS, target=chip, frequency=8_000_000)
    try:
        sim = avr8sharp.board(chip)
    except Exception:
        pytest.skip(f"avr8sharp has no simulation preset for {chip}")

    try:
        sim.with_hex(firmware)
        sim.run_ms(1)
        assert sim.cpu.sp == ramend
    finally:
        sim.close()


def geometry() -> list[tuple[str, int, int]]:
    """(chip, RAM_START, RAM_SIZE) for every AVR chip PyMCU ships."""
    chips_dir = pymcu_compiler.REPO_ROOT.parent / "pymcu" / "lib" / "src" / "pymcu" / "chips"
    out = []
    for path in sorted(chips_dir.glob("*.py")):
        text = path.read_text()
        if not re.search(r'device_info\([^)]*arch\s*=\s*"avr"', text):
            continue
        start = re.search(r"^RAM_START\s*=\s*(0x[0-9A-Fa-f]+|\d+)", text, re.M)
        size = re.search(r"^RAM_SIZE\s*=\s*(0x[0-9A-Fa-f]+|\d+)", text, re.M)
        out.append((path.stem, int(start.group(1), 0), int(size.group(1), 0)))
    return out


@pytest.mark.parametrize("chip,ram_start,ram_size",
                         [pytest.param(c, s, z, id=c) for c, s, z in geometry()])
def test_the_call_stack_stays_inside_the_bytes_reserved_for_it(chip, ram_start, ram_size):
    """The SRAM check reserves min(64, SRAM // 2) bytes at the top for the call stack and
    hands everything below to static data. This is the other half of that promise: a program
    that actually makes calls must keep its stack pointer inside the reservation.

    It is the measurement the reservation rests on. Shrinking the floor without this is a
    guess, and guessing low means the stack grows into the data with no diagnostic at all.
    The ATtiny13 and 13a are the parts that changed, and they are in this matrix.
    """
    reserve = min(64, ram_size // 2)
    floor = ram_start + ram_size - reserve

    firmware = pymcu_compiler.build_source(CALLS_A_FUNCTION, target=chip, frequency=8_000_000)
    try:
        sim = avr8sharp.board(chip)
    except Exception:
        pytest.skip(f"avr8sharp has no simulation preset for {chip}")

    try:
        sim.with_hex(firmware)
        lowest = ram_start + ram_size - 1
        for _ in range(400):
            sim.run_instructions(25)
            lowest = min(lowest, sim.cpu.sp)
        assert lowest >= floor, (
            f"{chip}: the stack reached 0x{lowest:04X}, below the 0x{floor:04X} floor that "
            f"{reserve} reserved bytes promise; static data is allowed to live up to there"
        )
    finally:
        sim.close()


def _statics(n: int) -> str:
    """A program whose static data is exactly n bytes: an n-1 byte array and one index."""
    return (
        "from pymcu.types import uint8\n"
        "\n"
        "\n"
        "def main():\n"
        f"    buf: uint8[{n - 1}] = [0] * {n - 1}\n"
        "    i: uint8 = 0\n"
        "    while True:\n"
        "        buf[i] = i\n"
        "        i = i + 1\n"
        "\n"
        "\n"
        "main()\n"
    )


# The two parts the reservation change is about. Half of 64 is 32, so 32 bytes of static
# data fit and 33 do not. Both sides are asserted: the accept side is the bug being fixed,
# and the refuse side is the check still doing its job one byte later. Without the refuse
# side, deleting the check entirely would pass.
@pytest.mark.parametrize("chip", ["attiny13", "attiny13a"])
def test_a_sixty_four_byte_part_splits_its_sram_in_half(chip):
    assert pymcu_compiler.build_source(_statics(32), target=chip, frequency=8_000_000)

    with pytest.raises(Exception) as refused:
        pymcu_compiler.build_source(_statics(33), target=chip, frequency=8_000_000)
    assert "the call stack needs 32 of them" in str(refused.value)


# Every part with 128 bytes or more keeps the 64 it always had, because half of 128 is
# already 64. This is the "nothing else moved" half of the change: an ATmega328P must still
# refuse at exactly the same byte it refused at before.
def test_a_larger_part_keeps_the_sixty_four_it_always_had():
    ram = 2048  # atmega328p
    assert pymcu_compiler.build_source(_statics(ram - 64), target="atmega328p")

    with pytest.raises(Exception) as refused:
        pymcu_compiler.build_source(_statics(ram - 63), target="atmega328p")
    assert "the call stack needs 64 of them" in str(refused.value)
