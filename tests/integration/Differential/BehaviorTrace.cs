using AVR8Sharp.Core.Peripherals;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Differential;

/// <summary>One effective output change on a GPIO port, as seen from outside the chip.</summary>
/// <param name="Port">Port letter — <c>'B'</c>, <c>'C'</c> or <c>'D'</c>.</param>
/// <param name="Value">The new effective output byte (PORT masked by DDR, with timer overrides applied).</param>
public readonly record struct PinEvent(char Port, byte Value)
{
    public override string ToString() => $"PORT{Port}=0x{Value:X2}";
}

/// <summary>
/// The chip state a program has published when it reaches an <c>asm("BREAK")</c> checkpoint.
/// </summary>
/// <remarks>
/// Many compiler fixtures produce no UART output at all: they compute a value, park it in a
/// general-purpose I/O register, and halt at a BREAK for the test to read (see
/// <c>BreakEdgesTests</c>). GPIOR0/1/2 and the three port registers are architectural
/// addresses, identical in both builds, which makes them the one piece of chip state that
/// can be compared across two independent compilations.
/// </remarks>
public readonly record struct Checkpoint(byte Gpior0, byte Gpior1, byte Gpior2, byte PortB, byte PortC, byte PortD)
{
    public override string ToString() =>
        $"GPIOR0=0x{Gpior0:X2} GPIOR1=0x{Gpior1:X2} GPIOR2=0x{Gpior2:X2} " +
        $"PORTB=0x{PortB:X2} PORTC=0x{PortC:X2} PORTD=0x{PortD:X2}";

    /// <summary>Names the fields that differ between two checkpoints.</summary>
    public string DifferenceFrom(Checkpoint other)
    {
        var parts = new List<string>();
        void Compare(string name, byte mine, byte theirs)
        {
            if (mine != theirs) parts.Add($"{name} 0x{mine:X2} vs 0x{theirs:X2}");
        }
        Compare("GPIOR0", Gpior0, other.Gpior0);
        Compare("GPIOR1", Gpior1, other.Gpior1);
        Compare("GPIOR2", Gpior2, other.Gpior2);
        Compare("PORTB", PortB, other.PortB);
        Compare("PORTC", PortC, other.PortC);
        Compare("PORTD", PortD, other.PortD);
        return string.Join(", ", parts);
    }
}

/// <summary>
/// Everything a differential run observes about a firmware image from outside the chip:
/// the bytes it transmitted over UART, the sequence of levels it drove on the GPIO ports,
/// and the state it published at each BREAK checkpoint. Deliberately excludes anything the
/// optimizer is allowed to change — cycle counts, instruction counts, flash size — so that
/// a difference between two traces of the same program is a difference in *behaviour*, not
/// in speed.
/// </summary>
public sealed class BehaviorTrace
{
    public required byte[] Uart { get; init; }
    public required IReadOnlyList<PinEvent> Pins { get; init; }
    public required IReadOnlyList<Checkpoint> Checkpoints { get; init; }

    /// <summary>Non-null when the simulation faulted (e.g. PC ran off the end of flash).</summary>
    public string? Crash { get; init; }

    /// <summary>Simulated cycles consumed. Diagnostic only — never compared.</summary>
    public long Cycles { get; init; }

    /// <summary>True when the run ended because it hit its stop target rather than the time budget.</summary>
    public bool ReachedTarget { get; init; }

    public bool IsSilent => Uart.Length == 0 && Pins.Count == 0 && Checkpoints.Count == 0 && Crash == null;
}

/// <summary>How long a traced run may go on, and how much observable output is enough.</summary>
/// <param name="MaxMs">Ceiling in simulated milliseconds.</param>
/// <param name="UartBytes">Stop once this many UART bytes have been captured.</param>
/// <param name="PinEvents">Stop once this many GPIO changes have been captured.</param>
/// <param name="Checkpoints">Stop once this many BREAK checkpoints have been captured.</param>
public readonly record struct TraceBudget(double MaxMs, int UartBytes, int PinEvents, int Checkpoints)
{
    /// <summary>
    /// One second of simulated time. Enough for the delay_ms-paced showcase programs to
    /// go round their main loop several times, and far more than the compute-and-print
    /// fixtures need; those stop early on the byte target.
    /// </summary>
    public static readonly TraceBudget Default =
        new(MaxMs: 1000, UartBytes: 512, PinEvents: 512, Checkpoints: 64);
}

public static class BehaviorRecorder
{
    /// <summary>
    /// Runs <paramref name="hexContent"/> on a fresh Arduino Uno simulation and records
    /// what it does to the outside world.
    /// </summary>
    /// <remarks>
    /// No peripherals beyond the board's own are attached and no UART input is injected:
    /// both variants of a program meet exactly the same environment, which is all the
    /// comparison needs. A program that stalls waiting for an ADC conversion or a serial
    /// byte stalls identically in both, and shows up as a silent trace rather than as a
    /// difference.
    /// </remarks>
    public static BehaviorTrace Record(string hexContent, TraceBudget budget)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hexContent);

        var pins = new List<PinEvent>();
        Register(uno.PortB, 'B');
        Register(uno.PortC, 'C');
        Register(uno.PortD, 'D');

        void Register(AvrIoPort port, char letter)
            => port.AddListener((value, _) =>
            {
                // Cap the list so a fast-toggling pin cannot grow it without bound while
                // the run waits on its UART target.
                if (pins.Count < budget.PinEvents) pins.Add(new PinEvent(letter, value));
            });

        var checkpoints = new List<Checkpoint>();

        // Evaluated before every instruction, so a BREAK is seen exactly once: it is captured
        // while the PC still sits on it, then executed like any other opcode and left behind.
        bool StopOrCapture()
        {
            if (uno.Cpu.Pc < uno.Cpu.ProgramMemory.Length &&
                uno.Cpu.ProgramMemory[(int)uno.Cpu.Pc] == BreakOpcode &&
                checkpoints.Count < budget.Checkpoints)
                checkpoints.Add(Snapshot(uno));

            return uno.Serial.ByteCount >= budget.UartBytes
                || pins.Count >= budget.PinEvents
                || checkpoints.Count >= budget.Checkpoints;
        }

        string? crash = null;
        var reachedTarget = false;
        try
        {
            uno.RunUntilMs(_ => StopOrCapture(), budget.MaxMs);
            reachedTarget = true;
        }
        catch (TimeoutException)
        {
            // Budget exhausted: an expected outcome for a program that idles or paces
            // itself with delays. The trace captured so far is still comparable.
        }
        catch (Exception ex)
        {
            crash = ex.Message;
        }

        return new BehaviorTrace
        {
            Uart = uno.Serial.Bytes,
            Pins = pins,
            Checkpoints = checkpoints,
            Crash = crash,
            Cycles = (long)uno.Cpu.Cycles,
            ReachedTarget = reachedTarget,
        };
    }

    private const ushort BreakOpcode = 0x9598;

    // ATmega328P data-space addresses, the same ones the BREAK-checkpoint fixtures use.
    private const int Gpior0 = 0x3E, Gpior1 = 0x4A, Gpior2 = 0x4B;
    private const int PortB = 0x25, PortC = 0x28, PortD = 0x2B;

    private static Checkpoint Snapshot(ArduinoUnoSimulation uno) => new(
        uno.Data[Gpior0], uno.Data[Gpior1], uno.Data[Gpior2],
        uno.Data[PortB], uno.Data[PortC], uno.Data[PortD]);
}
