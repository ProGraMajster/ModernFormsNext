# Exact control animation endpoints — #88 follow-up

The 2026-11-12 full Debug run for #165 passed 5930 of 5931 tests. The existing
native Windows `--tooltips` scenario failed `settled-presentation`. Its original
log and TRX are retained; the log did not record the residual transform value.
This failure is not counted as a passing run or attributed to #165 without evidence.

Investigation on master `818c47c28a063ad71251f085fc6bfd6c25f3885f`, before #165,
reproduced a concrete endpoint defect using the same production tooltip certification
fixture and a deterministic clock. A 120 ms quadratic ease-out transition sampled
at 119.6 or 119.8 ms retained translations of approximately 0.00006676 or 0.00001669
after completion. The transform setter treated the final zero as approximately equal
and discarded it. The native assertion requires exactly zero after settlement.
This establishes an existing defect consistent with the native failure; the original
native run did not capture enough state to prove its precise elapsed-time sequence.

All six public control render-transform setters used the same epsilon comparison.
They now skip only equal values. Opacity retains its existing clamp. Every distinct
assignment updates state and invalidates rendering/native presentation; no layout,
scheduler, duration, cancellation, accessibility, or popup ownership contract changes.
Small changes can now trigger repaint where they were previously dropped. The path
adds no allocations, and repeated equal assignments still avoid invalidation.

Regression coverage:

- `ToolTipTransitionTests.NearTerminalFrameStillCommitsTheExactTooltipEndpoint`
  uses sub-millisecond frames and the shared native certification fixture. The final
  advance is computed with `TimeSpan` subtraction so its ticks reach exactly 120 ms.
- `ControlAnimationEndpointTests` covers identity and non-identity endpoints,
  both opacity limits, all six properties, repaint, stable bounds, no layout,
  and repeated-value no-ops.
- Existing native HWND tooltip, focus/input, accessibility and rendered checks retain
  their original assertions. No retry, increased timeout, or relaxed tolerance is added.

The existing ControlGallery ToolTip panel remains the manual example. Automated
rendered/native tests do not claim a new human visual review, physical Android
qualification, or template verification. This change does not affect startup/templates.

Local raw evidence is retained under the ignored
`artifacts/scope-reaudit-2026-11-12/issue-88-endpoints/` directory. Initial whole-millisecond
test samples missed the epsilon interval; the first fractional test also exposed
double-to-TimeSpan truncation in its remaining-time calculation. Those unsuccessful
test-development runs are preserved separately from the final regression and full suite.
