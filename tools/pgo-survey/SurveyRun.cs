// SPDX-License-Identifier: MIT
// pymcuc-avr-pgo-survey, per-instruction collector run under ProfilingDecoder.
//
// The simulation runs in fixed-cycle quanta so the run can stop exactly on
// BREAK, stimulus bytes/pin edges can be injected between quanta, and the
// tail-window ("last 50% of cycles") analysis can run without storing a full
// execution trace.

using AVR8Sharp.Core;
using AVR8Sharp.Core.Decoders;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.AVR.PgoSurvey;

public sealed class BreakHit : Exception { }

public sealed class BackEdge
{
    public int BranchPc;      // word addr of the backward branch/jump
    public int Target;        // word addr of loop head (Target < BranchPc)
    public long Execs;        // times the branch instruction executed
    public long Taken;        // times the backward jump was actually made
    public long Entries;      // times pc arrived at Target from outside the loop
    public int  CurTrip;      // open consecutive-taken run
    public List<int> Trips = new();   // committed consecutive-taken run lengths
}

public sealed class Quantum
{
    public Dictionary<int, long> PcCycles = new();
    public Dictionary<int, HashSet<byte>>? IoReads;   // io data addr -> values read
    public long IsrCycles;
    public long Cycles;
}

public sealed class SurveyRun
{
    private readonly ArduinoUnoSimulation _sim;
    private readonly Cpu _cpu;
    private readonly Insn[] _meta;          // per-word decode, length = flash words
    private readonly SymbolTable _syms;
    private readonly int _vecEndWord;       // first word addr past the vector table
    private readonly int _mainWord;         // "main" symbol (for the bad-isr quirk)

    // ── Per-PC runtime counters ─────────────────────────────────────────
    public readonly long[] Exec;            // times each word-pc was executed
    public readonly long[] Cyc;             // cycles attributed to each word-pc
    public readonly long[] BranchTaken;     // cond-branch/skip: taken count
    public readonly long[] BranchNotTaken;
    public readonly Dictionary<int, HashSet<int>> MemAddrs = new();  // pc -> data addrs

    // ── Run-level counters ──────────────────────────────────────────────
    public long Instrs;
    public long IsrCycles;
    public int  IsrEntries;
    public string EndReason = "limit";      // limit | break | halt | crash
    public string? Crash;

    private bool _inIsr;
    private bool _pendingIsrExit;
    private int _badIsrWord = -1, _badIsrEnd;
    private int  _prevPc = -1;
    private ulong _prevCycles;
    private int  _pendBranch = -1;          // pc of conditional insn awaiting outcome
    private BackEdge? _pendEdge;            // back-edge awaiting taken/not-taken
    private bool _hitBreak;

    // per-quantum accumulation
    private readonly long[] _qCyc;
    private readonly List<int> _qPcs = new();
    private Dictionary<int, HashSet<byte>>? _qIo;
    private long _qIsr;
    public readonly List<Quantum> Quanta = new();

    public Cpu Cpu => _cpu;
    public Insn[] Meta => _meta;

    public readonly List<BackEdge> Edges = new();
    private readonly Dictionary<int, BackEdge> _edgeByBranch = new();
    private readonly Dictionary<int, List<BackEdge>> _edgeByHead = new();

    private ulong _nextUartTick, _nextPinTick;
    private bool _pinState;

    public SurveyRun(ArduinoUnoSimulation sim, SymbolTable syms)
    {
        _sim = sim;
        _cpu = sim.Cpu;
        _syms = syms;
        var prog = _cpu.ProgramMemory;
        _meta = new Insn[prog.Length];
        for (int w = 0; w < prog.Length; w++)
            _meta[w] = Insn.Decode((uint)w, prog[w],
                w + 1 < prog.Length ? prog[w + 1] : (ushort)0);

        Exec = new long[prog.Length];
        Cyc = new long[prog.Length];
        BranchTaken = new long[prog.Length];
        BranchNotTaken = new long[prog.Length];
        _qCyc = new long[prog.Length];

        // Vector table: the ATmega328P always gets 26 relaxed rjmp+pad slots,
        // i.e. words 0..51. __bad_interrupt is a loose symbol wherever the
        // linker placed it (it may sit inside the last slot's pad word, or far
        // below it with real code in between, both occur in the corpus).
        _vecEndWord = 52;
        _mainWord = _syms.AddrOf("main");
        _badIsrWord = _syms.AddrOf("__bad_interrupt");
        _badIsrEnd = _badIsrWord >= 0
            ? _syms.NextFuncAddrAfter(_badIsrWord) : 0;

        // Static backward edges: relative/absolute jumps whose static target
        // lies below the branch pc. Backward RCALL = recursion, not a loop.
        for (int w = 0; w < prog.Length; w++)
        {
            var m = _meta[w];
            if (m.Kind is InsnKind.BrCond or InsnKind.Rjmp or InsnKind.Jmp
                && m.TakenTarget >= 0 && m.TakenTarget < w)
            {
                var e = new BackEdge { BranchPc = w, Target = m.TakenTarget };
                Edges.Add(e);
                _edgeByBranch[w] = e;
                if (!_edgeByHead.TryGetValue(e.Target, out var l))
                    _edgeByHead[e.Target] = l = new();
                l.Add(e);
            }
        }
    }

    // ── Per-instruction callback (hot) ──────────────────────────────────
    public void OnInstruction(uint pcU, ulong cycles)
    {
        int pc = (int)pcU;
        Instrs++;
        Exec[pc]++;
        if (_prevPc >= 0)
        {
            var d = (long)(cycles - _prevCycles);
            Cyc[_prevPc] += d;
            _qCyc[_prevPc] += d;
            _qPcs.Add(_prevPc);            // duplicates deduped at quantum close
            if (_inIsr) { IsrCycles += d; _qIsr += d; }
        }

        // Outcome of the conditional branch / back-edge at the PREVIOUS pc,
        // resolved by where execution actually arrived.
        if (_pendBranch >= 0)
        {
            var pm = _meta[_pendBranch];
            if (pc == pm.TakenTarget) BranchTaken[_pendBranch]++;
            else if (pc == pm.FallTarget) BranchNotTaken[_pendBranch]++;
            // else an interrupt vectored in between: ambiguous, count neither.
            _pendBranch = -1;
        }
        if (_pendEdge != null)
        {
            var ed = _pendEdge;
            _pendEdge = null;
            if (pc == ed.Target) { ed.Taken++; ed.CurTrip++; }
            else if (pc < _vecEndWord) { /* IRQ stole the outcome, leave open */ }
            else
            {
                // Fell through the loop exit or escaped mid-body.
                if (ed.CurTrip > 0) { ed.Trips.Add(ed.CurTrip); ed.CurTrip = 0; }
            }
        }

        var m = _meta[pc];

        // BREAK: the opcode only raises an event; throw to stop the run here.
        if (m.Kind == InsnKind.Break) { _hitBreak = true; throw new BreakHit(); }

        // ISR entry: hardware jump to an even word inside the vector table
        // that is not the sequential next pc. pc == 0 is the reset vector.
        if (pc > 0 && pc < _vecEndWord && (pc & 1) == 0
            && _prevPc >= 0 && pc != _prevPc + _meta[_prevPc].Words)
        {
            _inIsr = true;
            IsrEntries++;
        }
        // Bad-interrupt trampoline: an unhandled IRQ vectors to a slot that
        // rjmp's to __bad_interrupt, which then falls through / jumps into the
        // next function (usually, but not always, main). Once execution leaves
        // the __bad_interrupt region the interrupt window is over.
        if (_inIsr && _badIsrWord >= 0
            && _prevPc >= _badIsrWord && _prevPc < _badIsrEnd
            && (pc < _badIsrWord || pc >= _badIsrEnd))
            _inIsr = false;
        if (_inIsr && pc == _mainWord && pc != _prevPc + _meta[_prevPc].Words)
            _inIsr = false;
        if (_pendingIsrExit) { _inIsr = false; _pendingIsrExit = false; }
        if (m.Kind == InsnKind.Reti) _pendingIsrExit = true;

        // Back-edge executed: outcome resolves on the next instruction.
        if (_edgeByBranch.TryGetValue(pc, out var edge))
        {
            edge.Execs++;
            _pendEdge = edge;
        }
        // Landed on a loop head from outside the loop = new trip entry.
        if (_prevPc >= 0 && _edgeByHead.TryGetValue(pc, out var heads))
            foreach (var e in heads)
                if (_prevPc < e.Target || _prevPc > e.BranchPc)
                    e.Entries++;

        if (m.IsCondBranch) _pendBranch = pc;
        if (m.IsMemRead || m.IsMemWrite) TrackMem(pc, in m);

        _prevPc = pc;
        _prevCycles = cycles;
    }

    private void TrackMem(int pc, in Insn m)
    {
        int addr;
        var data = _cpu.Mmio.Data;
        switch (m.Base)
        {
            case MemBase.Abs:       addr = m.Addr; break;
            case MemBase.X:         addr = data[26] | (data[27] << 8); break;
            case MemBase.Y:         addr = (data[28] | (data[29] << 8)) + m.Addr; break;
            case MemBase.Z:         addr = (data[30] | (data[31] << 8)) + m.Addr; break;
            case MemBase.StackPush: addr = _cpu.Sp; break;
            case MemBase.StackPop:  addr = _cpu.Sp + 1; break;
            case MemBase.None:
                if (m.IsIoAccess) addr = m.Addr + 0x20;   // io space -> data space
                else return;
                break;
            default: return;
        }

        if (!MemAddrs.TryGetValue(pc, out var set))
            MemAddrs[pc] = set = new HashSet<int>();
        set.Add(addr);

        // I/O reads: data-space addresses below SRAM start (0x100 on the
        // ATmega328P). Record the value about to be read for stall analysis.
        if (m.IsMemRead && addr < 0x100)
        {
            byte v = data[addr];
            _qIo ??= new Dictionary<int, HashSet<byte>>();
            if (!_qIo.TryGetValue(addr, out var vs)) _qIo[addr] = vs = new();
            vs.Add(v);
        }
    }

    // ── Run loop ────────────────────────────────────────────────────────
    public void Run(ulong totalCycles, uint freqHz, bool stimulus)
    {
        ulong quantum = Math.Max((ulong)(freqHz / 2000), 1000);   // 0.5 ms
        var decoder = new ProfilingDecoder(OnInstruction);
        int haltQuanta = 0;

        _nextUartTick = (ulong)(freqHz / 1000);          // first byte at t=1ms
        _nextPinTick  = (ulong)(freqHz / 1000) * 5;      // pins every 5 ms

        while (_cpu.Cycles < totalCycles && !_hitBreak)
        {
            var step = (long)Math.Min(quantum, totalCycles - _cpu.Cycles);
            try
            {
                _sim.RunCyclesProfiled(step, decoder);
            }
            catch (BreakHit) { /* stopped exactly at BREAK */ }
            catch (Exception ex)
            {
                EndReason = "crash";
                Crash = ex.GetType().Name + ": " + ex.Message;
                CommitQuantum();
                break;
            }
            CommitQuantum();
            if (_hitBreak) { EndReason = "break"; break; }
            if (stimulus) ApplyStimulus(freqHz);

            // Early exit when parked in a named halt spin for two consecutive
            // quanta, the program is finished, not stalled.
            var lastQ = Quanta[^1];
            if (lastQ.PcCycles.Count <= 4 && AllPcInHalt(lastQ))
            {
                if (++haltQuanta >= 2) { EndReason = "halt"; break; }
            }
            else haltQuanta = 0;
        }

        // Commit still-open trips.
        foreach (var e in Edges)
            if (e.CurTrip > 0) { e.Trips.Add(e.CurTrip); e.CurTrip = 0; }
    }

    private void CommitQuantum()
    {
        var q = new Quantum { IsrCycles = _qIsr };
        foreach (var pc in _qPcs)
            if (!q.PcCycles.ContainsKey(pc))
            {
                q.PcCycles[pc] = _qCyc[pc];
                _qCyc[pc] = 0;
            }
        q.IoReads = _qIo;
        q.Cycles = q.PcCycles.Values.Sum();
        Quanta.Add(q);
        _qPcs.Clear();
        _qIo = null;
        _qIsr = 0;
    }

    private bool AllPcInHalt(Quantum q)
    {
        if (q.PcCycles.Count == 0) return false;
        foreach (var pc in q.PcCycles.Keys)
            if (!IsHaltPc(pc)) return false;
        return true;
    }

    private bool IsHaltPc(int pc) =>
        _meta[pc].Kind == InsnKind.HaltJump
        || _syms.ContainingFunction(pc)
            is "__pymcu_halt_spin" or "__exn_halt" or "_exit" or "__exit";

    private void ApplyStimulus(uint freqHz)
    {
        // UART RX byte 0x41 ('A') every 1 ms, delivered with realistic frame
        // timing (the USART model completes the frame after CyclesPerChar).
        if (_cpu.Cycles >= _nextUartTick)
        {
            _sim.Serial.InjectByte(0x41);
            _nextUartTick += (ulong)(freqHz / 1000);
        }
        // Toggle PD2..PD7 every 5 ms.
        if (_cpu.Cycles >= _nextPinTick)
        {
            _pinState = !_pinState;
            for (byte p = 2; p <= 7; p++)
                _sim.PortD.SetPinValue(p, _pinState);
            _nextPinTick += (ulong)(freqHz / 1000) * 5;
        }
    }
}
