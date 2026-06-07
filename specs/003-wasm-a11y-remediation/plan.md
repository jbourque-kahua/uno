# Implementation Plan: WASM Accessibility Remediation

**Branch**: `003-wasm-a11y-remediation` | **Date**: 2026-06-06 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/003-wasm-a11y-remediation/spec.md`
**Grounding**: [research.md](./research.md) (two adversarially-verified audits)

## Summary

Remediate defects found by auditing the existing Skia-on-WebAssembly accessibility layer:
fix the **broken** RadioButton mapping, gate **tabindex** on real focusability so
non-interactive elements (headings, composite containers) leave the tab order, close
**live-sync** gaps (PasswordBox value, heading level, placeholder, scrollability), correct
**heading level 7–9** handling, make **ScrollViewer** regions meaningful, decide **body
text** exposure, and establish **DOM-level runtime tests** (the current suite is almost
entirely `[Ignore]`d). Target is the Skia WASM AOM; the native WASM-DOM target is
maintenance-only.

## Technical Context

**Language/Version**: C# (.NET 9.0/10.0), TypeScript (compiled to embedded JS resource)
**Primary Dependencies**: Uno.UI (automation peers, `AriaMapper`), Uno.UI.Runtime.Skia
(`SkiaAccessibilityBase`, `IsAccessibilityFocusable`), Uno.UI.Runtime.Skia.WebAssembly.Browser
(`WebAssemblyAccessibility`, `SemanticElementFactory`, `Accessibility.ts`,
`SemanticElements.ts`, `FocusSynchronizer`), `System.Runtime.InteropServices.JavaScript`
(JSImport/JSExport)
**Storage**: N/A (runtime accessibility layer)
**Testing**: `src/Uno.UI.RuntimeTests` on Skia WASM (AOM enabled in-test via
`EnableAccessibilityThroughDom`); manual NVDA/VoiceOver; axe-core for landmark/name checks
**Target Platform**: WebAssembly (Skia). Native WASM-DOM target: out of scope (no regressions)
**Project Type**: Framework library extension (no new projects)
**Performance Goals**: No per-frame regression; attribute updates batch within one frame
(reuse/activate the existing 100ms debounce only if it does not delay correctness)
**Constraints**: Cannot change public automation-peer interfaces (cross-platform); changes
under `Uno.UI` shared code must consult WinUI sources (Constitution VII) and not break
native targets; TypeScript compiles to an embedded resource
**Scale/Scope**: 6 mappings + cross-cutting tabindex, live-sync, and ARIA-attribute
correctness (30 FRs); ~7 C# files, 2 TS files, ~9 runtime-test classes

### NEEDS CLARIFICATION (resolve before/within Phase 1)

- **Body-text exposure** (FR-015): keep pruning + parent-absorb only, OR additionally emit
  a `<p>`/`<span>` for standalone text. Recommendation: gated standalone emission. *Owner
  decision.*
- **ScrollViewer region liveness** (FR-013): is runtime scrollability-transition sync
  required, or is creation-time gating sufficient for v1? Recommendation: creation-time
  gate now, live-transition deferred.
- **Composite tabindex model** (FR-007): roving active-item vs. container +
  activedescendant. Recommendation: roving (matches existing item roving + DOM `.focus()`).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. WinUI API Fidelity | PASS | No public API changes; uses existing automation-peer surface. RadioButton fix consults UIA radio semantics. |
| II. Cross-Platform Parity | PASS | Changes isolated to Skia WASM (`.wasm.cs` / Browser project / shared `AriaMapper` used by Skia). Native targets unaffected; `AriaMapper` change (RadioButton initial state) must be verified not to regress other Skia hosts. |
| III. Test-First Quality Gates | PASS (enforced) | This feature is *partly about* tests — each fix lands with a fails-before/passes-after runtime test; `[Ignore]`d suite re-enabled. |
| IV. Performance Discipline | PASS | Hot path is element creation/attribute update; threading `isFocusable` adds one bool. No new per-frame allocation. Avoid double `GetSemanticElementType`. |
| V. Generated Code Boundaries | PASS | No `Generated/` edits. |
| VI. Backward Compatibility | PASS | Additive/bugfix; no breaking API. tabindex changes alter behavior but fix WCAG violations (document in release notes). |
| VII. WinUI Implementation Alignment | APPLIES | Consult WinUI C++ for radio selection semantics, heading-level mapping, and region/landmark rules. Ask for source location. |

**Gate Status**: PASS — no violations requiring Complexity Tracking. One watch-item:
the RadioButton initial-state fix touches shared `AriaMapper` (Uno.UI), so it affects all
Skia hosts, not just WASM — validate on Skia Desktop too.

## Project Structure

### Documentation (this feature)

```text
specs/003-wasm-a11y-remediation/
├── plan.md              # This file
├── research.md          # Phase 0 — consolidated audit findings (DONE)
├── spec.md              # Feature spec (DONE)
├── data-model.md        # Phase 1 — entities/state model
├── quickstart.md        # Phase 1 — how to build/enable AOM/run tests
├── contracts/
│   └── interop-contracts.md   # Phase 1 — changed JSImport/JSExport signatures
├── checklists/          # (optional) review checklists
└── tasks.md             # Phase 2 — /speckit.tasks (NOT created here)
```

### Source Code (repository root)

```text
src/Uno.UI/Accessibility/
└── AriaMapper.cs                         # RadioButton initial Checked; heading level; populate LabelledBy; LocalizedControlType + LocalizedLandmarkType(all landmarks)→roledescription; Level; aria-invalid/orientation; role normalization (MODIFY)

src/Uno.UI/UI/Xaml/Automation/
└── AutomationProperties.uno.cs           # FindHtmlRole invalid-token normalization (shared, also fixes native); AutomationId→DOM id (MODIFY)

src/Uno.UI.Runtime.Skia/Accessibility/
└── SkiaAccessibilityBase.cs              # IsAccessibilityFocusable reuse; base update hooks (REFERENCE/MODIFY)

src/Uno.UI.Runtime.Skia.WebAssembly.Browser/Accessibility/
├── WebAssemblyAccessibility.cs           # NotifyPropertyChangedEventCore branches; radio routing; thread isFocusable; region gating; roving driver; generic-path attribute parity; AutomationId-not-aria-label; IDREF integrity (MODIFY)
├── SemanticElementFactory.cs             # Add isFocusable to Create* signatures; radio checked; region gating; IDREF existence check (MODIFY)
└── FocusSynchronizer.cs                  # Drive roving tabindex on focus movement (MODIFY)

src/Uno.UI.Runtime.Skia.WebAssembly.Browser/ts/Runtime/
├── SemanticElements.ts                   # Honor isFocusable; remove heading tabIndex; container/composite tabindex model; radio checked; aria-orientation; live-update fns (MODIFY)
└── Accessibility.ts                      # updateElementFocusability reuse; generic-path parity; aria-labelledby; AutomationId→data-* not aria-label; IDREF validation (MODIFY)

src/Uno.UI/UI/Xaml/Controls/PasswordBox/
└── PasswordBox.cs (+ PasswordBoxAutomationPeer.cs)  # Raise value automation event for live-sync (MODIFY)

src/Uno.UI.RuntimeTests/Tests/Windows_UI_Xaml_Automation/
├── Given_AccessibleButton.cs             # Re-enable + DOM-level asserts (MODIFY)
├── Given_AccessibleCheckBox.cs           # Re-enable; add RadioButton DOM tests (MODIFY)
├── Given_AccessibleTextBox.cs            # Add PasswordBox live-sync, element-type DOM tests (MODIFY)
├── Given_AccessibleHeading.cs            # Heading element/level/tabindex DOM tests (NEW)
├── Given_AccessibleScrollViewer.cs       # Region gating/label DOM tests (NEW)
├── Given_AccessibleTabindex.cs           # Cross-cutting: non-interactive ≠ tab stop (NEW)
├── Given_AccessibleAria.cs               # ARIA attr correctness: AutomationId/LabeledBy/role/IDREF/generic-parity (NEW)
├── Given_AccessibleLandmark.cs           # Landmark role + LocalizedLandmarkType→roledescription + region-must-have-name (NEW)
└── Given_AccessibleListView.cs           # Re-enable composite tabindex/roving (MODIFY)
```

**Structure Decision**: Extend the existing 001/002 surface; no new projects. TS compiles
to the embedded resource. Tests live in the existing automation runtime-test folder.

## Phased delivery (suggested PR sequence)

Ordered by severity and independent testability (each phase is shippable):

1. **Phase A — RadioButton fix (P1).** FR-001..004. `AriaMapper` initial `checked`; radio
   DOM activation → select; external-change → native `checked`; radio roving at creation.
   Tests: `Given_AccessibleCheckBox` radio cases (active, DOM-level). *Highest impact,
   self-contained.*
2. **Phase B — tabindex gating (P1).** FR-005..008. Thread `isFocusable` through the
   factory; remove heading `tabIndex`; one consistent composite model; disabled-composite
   detabbing. Tests: `Given_AccessibleTabindex` (NEW) + heading/listbox/tab/menu.
3. **Phase C — heading correctness (P2).** FR-011..012 + heading live-sync (part of FR-009).
   Level 7–9 passthrough; `aria-level` live update; roving driven by focus. Tests:
   `Given_AccessibleHeading` (NEW).
4. **Phase D — live-sync gaps (P2).** FR-009..010. PasswordBox value raise + branch;
   placeholder; `aria-required`. Consider a generalized property→attribute map. Tests:
   extend `Given_AccessibleTextBox`.
5. **Phase E — ScrollViewer region + body text (P3).** FR-013..015. Gate region on
   scrollable+named; meaningful label; body-text decision. Tests: `Given_AccessibleScrollViewer`
   (NEW). *Blocked on the two open decisions.*
6. **Phase F — test re-enablement + axe pass (P2, cross-cutting).** FR-016..017. Re-enable
   the `[Ignore]`d suite; add the axe/landmark checks for SC-006.
7. **Phase G — ARIA attribute correctness & path parity (P1 for wrong-target/role; P2 for
   the rest).** FR-018..030. Split into two shippable slices:
   - **G1 (P1):** stop sourcing `aria-label` from `AutomationId` (FR-018); emit
     `aria-labelledby` from `LabeledBy` (FR-019); normalize `FindHtmlRole` to valid ARIA
     roles (FR-020, shared C# — validate native path too); factory↔generic attribute parity
     (FR-021); dangling-IDREF integrity (FR-022). Tests: `Given_AccessibleAria` (NEW).
   - **G2 (P2):** `aria-invalid` (FR-023), `aria-orientation` (FR-024),
     `aria-roledescription` from `LocalizedControlType` **and** `LocalizedLandmarkType` on
     all landmark types (FR-025), landmark/region-must-have-a-name + no-roledescription-
     without-name (FR-014), standalone `Level` (FR-026), the missing live-sync branches
     incl. `LandmarkType`/`LocalizedLandmarkType` — which also need a changed-callback wired
     (FR-027, dovetails with Phase D / FR-010), value-semantics corrections (FR-028), and the
     lower-priority completeness gaps (FR-029). Tests: `Given_AccessibleLandmark` (NEW).

Phases A, B, and G1 are P1 and largely independent — they can land first/parallel. C–E build
on B's gating mechanism; G2 dovetails with D's generalized property→attribute map. F runs
continuously and is finalized last. Note the shared-code watch-item below now also covers
`AriaMapper`/`AutomationProperties.uno.cs` role normalization (affects all Skia hosts + the
native path).

## Complexity Tracking

> No constitution violations — table not required. Watch-items (shared `Uno.UI` code,
> affects all Skia hosts and — for role normalization — the native WASM-DOM path; validate
> on Skia Desktop and a native-WASM smoke in addition to Skia WASM):
> - RadioButton initial-state change in `AriaMapper` (FR-001).
> - `FindHtmlRole` role-token normalization in `AutomationProperties.uno.cs` (FR-020) — must
>   improve, never regress, native role output.
> - PasswordBox value-raise in shared `PasswordBox`/peer (FR-009).

---

## Phase 0: Research

See [research.md](./research.md) — complete. All findings adversarially verified. Three
NEEDS CLARIFICATION items (body-text, region liveness, composite model) are surfaced with
recommendations; they gate Phase E (and the composite model gates Phase B's container
decision).

## Phase 1: Design & Contracts

- **Data model** → [data-model.md](./data-model.md): the two creation paths, the
  focusability gate, the roving model, and the property→attribute update map.
- **Contracts** → [contracts/interop-contracts.md](./contracts/interop-contracts.md): the
  changed JSImport (`Create*Element` gaining `isFocusable`; new/updated update fns) and
  JSExport (radio selection routing) signatures — the C#↔TS boundary that must stay in sync.
- **Quickstart** → [quickstart.md](./quickstart.md): build the WASM target, enable the AOM
  in a runtime test, inspect `uno-semantics-{handle}`, run the suite.
- **Agent context**: this plan is referenced from `CLAUDE.md` between the SPECKIT markers.

Re-evaluate Constitution Check after design: still PASS (no new public API; tests-first
retained; performance neutral).
