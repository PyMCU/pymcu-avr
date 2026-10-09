"""Contract tests for the oracle harness itself.

`test_probe_matches_cpython_or_refuses_as_documented` must accept exactly the
outcome a probe's `# expect:` header asks for -- a `refuse` probe that compiles
is a silently miscompiled program, and a `match`/`divergence` probe that only
built never compared outputs. Nothing here compiles: `evaluate_probe` and
`pymcu_bin` are stubbed, so these tests pin the assertion, not the toolchain.
"""
from __future__ import annotations

import functools
import re
from pathlib import Path

import pytest

import test_oracle


@pytest.mark.parametrize(
    "expect_header, outcome, accepted",
    [
        ("refuse some diagnostic", "refused", True),
        ("refuse some diagnostic", "refused with different diagnostic", False),
        ("refuse some diagnostic", "compiled", False),
        ("refuse some diagnostic", "match", False),
        ("compile", "compiled", True),
        ("compile", "compile-fail", False),
        ("compile", "refused", False),
        ("match", "match", True),
        ("match", "compiled", False),
        ("match", "compile-fail", False),
        ("match", "mismatch", False),
        ("divergence https://docs.pymcu.org/roadmap/#language", "match", True),
        ("divergence https://docs.pymcu.org/roadmap/#language", "compiled", False),
        ("divergence https://docs.pymcu.org/roadmap/#language", "mismatch", False),
    ],
)
def test_probe_outcome_is_exactly_what_the_expectation_demands(
    expect_header: str,
    outcome: str,
    accepted: bool,
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    request: pytest.FixtureRequest,
):
    probe = tmp_path / "p9999_contract_probe.py"
    probe.write_text(f"# expect: {expect_header}\n# doc: harness contract\n")
    fake = test_oracle.OracleOutcome(
        probe.name, "contract", expect_header, outcome, "stubbed"
    )
    monkeypatch.setattr(test_oracle, "evaluate_probe", lambda *_args: fake)
    monkeypatch.setattr(test_oracle, "pymcu_bin", lambda: probe)
    run = functools.partial(
        test_oracle.test_probe_matches_cpython_or_refuses_as_documented,
        probe=probe,
        tmp_path=tmp_path / "work",
        request=request,
        avr_runner=probe,
    )
    if accepted:
        run()
    else:
        with pytest.raises(AssertionError, match=re.escape(outcome)):
            run()
