// SPDX-License-Identifier: MIT
// PyMCU AVR Backend — object lifetime for arrays that escape their frame.

using PyMCU.IR;

namespace PyMCU.Backend.Targets.AVR;

/// <summary>
/// Promotes SRAM arrays whose storage outlives the frame that created them into
/// <see cref="ProgramIR.GlobalArrays"/>, so the call-tree overlay in
/// StackAllocator can never reuse their bytes for a sibling subtree's locals.
///
/// The allocator gives every array it meets a slot inside the frame of a
/// function that names it, and sibling call subtrees deliberately share one
/// region. That is correct only while the array dies with its frame. An array
/// that escapes — `self.buf = bytearray(n)` stored on an instance in a
/// constructor, read back by a later call — is ONE name in every function that
/// touches it, the last function allocated wins the shared offset, and a
/// sibling's locals land on the buffer's bytes. On the SSD1306 Life program the
/// 513-byte `display_buffer`, created in the inlined `__init__`, landed inside
/// the region `step()`'s frame reuses, so any store in step() corrupted the
/// framebuffer; symmetrically a store into the buffer corrupted step()'s
/// locals.
///
/// The frontend already sends module-level arrays and TYPED instance array
/// fields (`self.buf: uint8[n]`, #275) here through GlobalArrays. What never
/// reaches it is the untyped spelling (`self.buf = bytearray(n)`, the SSD1306
/// driver's shape) and every other way an address outlives its creator. This
/// pass reads the IR for the evidence rather than the source for the shape:
///
///  - the array is named by ArrayLoad/ArrayStore in two or more functions, or
///    its address is materialized (ArrayBase) in two or more functions — a
///    symbol only one body ever sees can only live in that body;
///  - the array's address is stored where a frame cannot take it back: into a
///    program global, into memory through a pointer or another array, into a
///    return value, into inline asm, into a GC root, or into a call whose
///    callee lets the parameter escape.
///
/// An array that never leaves its frame — created, used and dead inside one
/// function — keeps its overlaid slot: promotion is SRAM the overlay exists to
/// save, so the rules stay narrow on purpose.
/// </summary>
public static class AvrEscapingArrays
{
    /// <summary>
    /// Mutates <paramref name="program"/>: moves every escaping array into
    /// <see cref="ProgramIR.GlobalArrays"/>. Returns the promoted names.
    /// </summary>
    public static IReadOnlyCollection<string> Apply(ProgramIR program)
    {
        var globalNames = new HashSet<string>(
            program.Globals.Select(g => g.Name), StringComparer.Ordinal);
        var funcs = new Dictionary<string, Function>(StringComparer.Ordinal);
        foreach (var f in program.Functions) funcs[f.Name] = f;

        // refs[name]: functions that touch the storage by name or by address.
        var refs = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        // sizes[name]: largest count * elemSize seen, matching StackAllocator's formula.
        var sizes = new Dictionary<string, int>(StringComparer.Ordinal);
        // Arrays whose address reached a non-call sink; (array, callee, argIdx) for
        // addresses handed to a named call, resolved once param leaks are known.
        var escaped = new HashSet<string>(StringComparer.Ordinal);
        var arrayCallArgs = new List<(string Array, string Callee, int ArgIdx)>();
        // paramEdges: (caller, callerParam, callee, argIdx) — a caller's parameter
        // forwarded into a named call; feeds the leak fixpoint below.
        var paramEdges = new List<(string Caller, string Param, string Callee, int ArgIdx)>();
        // directLeaks[f]: params of f that reach a non-call sink or an opaque call.
        var directLeaks = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        // One forward scan per function. pointee[var] records what a local holds:
        // "a:<array>" for an address derived from ArrayBase, "p:<param>" for one
        // derived from a parameter — a Copy or address arithmetic moves the mark,
        // any other write drops it.
        void ScanFunction(Function func)
        {
            var pointee = new Dictionary<string, string>(StringComparer.Ordinal);
            // Parameters start marked "p:<param>": an address that arrived in one is
            // owned by the caller, so where it lands here decides the caller's array.
            foreach (var p in func.Params) pointee[p] = "p:" + p;

            string? PointeeOf(Val? v) => v switch
            {
                ArrayBase ab => "a:" + ab.ArrayName,
                Variable vv => pointee.GetValueOrDefault(vv.Name),
                Temporary tv => pointee.GetValueOrDefault(tv.Name),
                _ => null,
            };

            // The value `v` (already resolved through pointee) reached a position
            // where it persists past this frame.
            void Sink(Val? v)
            {
                var p = PointeeOf(v);
                if (p == null) return;
                if (p.StartsWith("a:", StringComparison.Ordinal))
                    escaped.Add(p.Substring(2));
                else
                    (directLeaks.TryGetValue(func.Name, out var s)
                        ? s : directLeaks[func.Name] = new HashSet<string>(StringComparer.Ordinal))
                        .Add(p.Substring(2));
            }

            // For named calls the escape depends on what the callee does with the
            // parameter; opaque targets (indirect, virtual, unknown name) leak always.
            void CallArgs(IReadOnlyList<Val> args, string? calleeName)
            {
                for (int i = 0; i < args.Count; i++)
                {
                    var p = PointeeOf(args[i]);
                    if (p == null) continue;
                    if (calleeName != null)
                    {
                        if (p.StartsWith("a:", StringComparison.Ordinal))
                            arrayCallArgs.Add((p.Substring(2), calleeName, i));
                        else
                            paramEdges.Add((func.Name, p.Substring(2), calleeName, i));
                    }
                    else
                    {
                        Sink(args[i]);
                    }
                }
            }

            void Derive(Val? dst, params Val?[] srcs)
            {
                var name = (dst as Variable)?.Name ?? (dst as Temporary)?.Name;
                if (name == null) return;
                var p = srcs.Select(PointeeOf).FirstOrDefault(x => x != null);
                if (p != null) pointee[name] = p;
                else pointee.Remove(name);
            }

            void Kill(Val? dst)
            {
                var name = (dst as Variable)?.Name ?? (dst as Temporary)?.Name;
                if (name != null) pointee.Remove(name);
            }

            void Touch(string arrayName, int size)
            {
                (refs.TryGetValue(arrayName, out var s)
                    ? s : refs[arrayName] = new HashSet<string>(StringComparer.Ordinal))
                    .Add(func.Name);
                if (size > sizes.GetValueOrDefault(arrayName)) sizes[arrayName] = size;
            }

            foreach (var instr in func.Body)
            {
                // The one exhaustive walker counts every ArrayBase this instruction
                // carries, whatever position it sits in.
                AvrGpiorPromotion.VisitVals(instr, v =>
                {
                    if (v is ArrayBase ab) Touch(ab.ArrayName, 0);
                });

                switch (instr)
                {
                    case ArrayLoad al:
                        Touch(al.ArrayName, al.Count * al.ElemType.SizeOf());
                        Kill(al.Dst);
                        break;
                    case ArrayStore ast:
                        Touch(ast.ArrayName, ast.Count * ast.ElemType.SizeOf());
                        if (PointeeOf(ast.Src) != null) Sink(ast.Src);
                        break;
                    // ArrayLoadFlash reads PROGMEM storage; it owns no SRAM slot and
                    // the overlay cannot touch it, so it is not counted at all.
                    case ArrayLoadFlash alf: Kill(alf.Dst); break;
                    case Copy c:
                        if (PointeeOf(c.Src) != null
                            && (c.Dst is Variable dv && globalNames.Contains(dv.Name)
                                || c.Dst is MemoryAddress))
                            Sink(c.Src);
                        Derive(c.Dst, c.Src);
                        break;
                    case Unary u: Derive(u.Dst, u.Src); break;
                    case Binary b: Derive(b.Dst, b.Src1, b.Src2); break;
                    case Bitcast bc: Derive(bc.Dst, bc.Src); break;
                    case Return r:
                        // Only a locally-created address is a leak here: returning a
                        // parameter hands it back to the frame that already owns it.
                        if (PointeeOf(r.Value) is { } rp && rp.StartsWith("a:", StringComparison.Ordinal))
                            escaped.Add(rp.Substring(2));
                        break;
                    case StoreIndirect si:
                        // Src stored through a pointer escapes; DstPtr pointing into
                        // the array writes into it during this frame, which is safe.
                        if (PointeeOf(si.Src) != null) Sink(si.Src);
                        break;
                    case LoadIndirect li: Kill(li.Dst); break;
                    case BytearrayLoad bl: Kill(bl.Dst); break;
                    case BytearrayStore bs:
                        if (PointeeOf(bs.Src) != null) Sink(bs.Src);
                        break;
                    case Call cl:
                        CallArgs(cl.Args, cl.FunctionName);
                        Kill(cl.Dst);
                        break;
                    case IndirectCall ic:
                        CallArgs(ic.Args, null);
                        Kill(ic.Dst);
                        break;
                    case VirtualCall vc:
                        // VisitVals does not reach into a vcall; Self is typed
                        // Variable, so a derived address there also escapes.
                        if (PointeeOf(vc.Self) != null) Sink(vc.Self);
                        CallArgs(vc.Args, null);
                        Kill(vc.Dst);
                        break;
                    case InlineAsm ia when ia.Operands != null:
                        foreach (var op in ia.Operands)
                            if (PointeeOf(op) != null) Sink(op);
                        break;
                    case GcRoot gr: Sink(gr.Var); break;
                    case GcUnroot gu: Sink(gu.Var); break;
                    case BitCheck bck: Kill(bck.Dst); break;
                    case GcAlloc ga: Kill(ga.Dst); break;
                    case FlashLoadPtr flp: Kill(flp.Dst); break;
                    case AugAssign aa:
                        // In-place: the target keeps whatever it already derived.
                        break;
                }
            }
        }

        foreach (var func in program.Functions) ScanFunction(func);

        // Leak fixpoint: a caller's param escapes when it is stored into globals or
        // memory, returned, rooted, handed to asm or an opaque call, or forwarded to
        // a callee whose own parameter at that position leaks.
        var leaks = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var f in program.Functions)
            leaks[f.Name] = directLeaks.TryGetValue(f.Name, out var d)
                ? new HashSet<string>(d, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (caller, param, callee, argIdx) in paramEdges)
            {
                if (leaks[caller].Contains(param)) continue;
                if (funcs.TryGetValue(callee, out var cf)
                    && argIdx < cf.Params.Count
                    && leaks[callee].Contains(cf.Params[argIdx]))
                {
                    leaks[caller].Add(param);
                    changed = true;
                }
            }
        }

        foreach (var (array, callee, argIdx) in arrayCallArgs)
        {
            // An address passed to a callee the program does not contain (extern)
            // can end up anywhere; so can one whose callee stashes the parameter.
            if (!funcs.TryGetValue(callee, out var cf)
                || argIdx >= cf.Params.Count
                || leaks[callee].Contains(cf.Params[argIdx]))
                escaped.Add(array);
        }

        var promoted = new List<string>();
        foreach (var (name, fns) in refs)
        {
            if (fns.Count < 2 && !escaped.Contains(name)) continue;
            if (program.GlobalArrays.ContainsKey(name)) continue;
            // No ArrayLoad/ArrayStore ever named it: its size never reached the IR,
            // so there is nothing to reserve. Its storage still exists where its
            // writer put it; only a fresh slot here would be a guess.
            if (!sizes.TryGetValue(name, out var size) || size <= 0) continue;
            program.GlobalArrays[name] = size;
            promoted.Add(name);
        }
        return promoted;
    }
}
