# Launcher UI design QA

## Evidence and target

- Source visual truth: `reference-codex-sidebar.png` (byte-identical to the supplied reference screenshot)
- Problem-state captures: `ux-audits/test-62-readability/01-home-dark.png` through `06-home-light.png`
- Final implementation: `ux-audits/test-62-readability/14-flights-light-rag-final.png` and `15-flights-dark-rag-final.png`; shell/layout evidence remains in `11-support-light-final.png` and `12-support-light-collapsed-final.png`
- Side-by-side comparison: `ux-audits/test-62-readability/16-reference-rag-comparison.png`
- Viewport: 1100 x 760 WPF window at the active Windows display density.
- Source pixels: 550 x 1874. The source rail was normalized to 223 x 760 for comparison.
- Implementation pixels: 1100 x 760. The implementation rail remained at its rendered 220 px width.
- Compared state: dark desktop rail and Flights tab with red/amber/green semantics; the same status mapping was checked separately in light mode.

## Full-view comparison

The final shell preserves the Codex reference's compact desktop-app structure: a flat persistent left rail, quiet charcoal outer surfaces, restrained violet selection, compact 36 px tab rows, a separated bottom status area, and content that begins immediately beside the rail. The source contains Codex-specific global navigation and project history; the launcher intentionally maps the structure to its three user tasks: Home, Flights, and Support.

## Focused comparison

The combined comparison image makes the important rail details readable without a second crop. Rail width, background tone, selected-row treatment, icon scale, text density, divider treatment, and footer placement align closely enough that no additional focused region was needed. The launcher uses chevrons for the requested collapse/expand affordance; the reference itself does not expose a collapsed state in the supplied screenshot.

## Required fidelity surfaces

- Fonts and typography: Segoe UI Variable/Segoe UI provides the correct Windows desktop character. Page titles, tab labels, status labels, and secondary copy were inspected in both themes. Explicit dynamic foregrounds prevent theme-switch contrast failures.
- Spacing and layout rhythm: the rail is 220 px expanded and 64 px collapsed, with 36 px tab rows and a 60 px footer. Main content uses 36 px horizontal padding and a 980 px working width. Cards use consistent 12 px radii and 14-18 px section gaps.
- Colors and tokens: dark and light surfaces follow the reference's neutral charcoal/lilac family. The user-directed semantic system is red for missing/not installed, amber for waiting and Ready, and green for detected/installed/healthy. Each theme uses a separate contrast-adjusted shade while preserving the same meaning.
- Image quality and assets: the packaged application icon is preserved. Navigation and support icons use one Windows system-glyph family; there are no placeholder images, handcrafted SVGs, emoji, or CSS-art substitutes.
- Copy and content: labels now follow user intent. Home covers setup and app management, Flights covers telemetry work, and Support contains help.
- Accessibility and states: tab names and collapse/expand descriptions are exposed to UI Automation. Expanded/collapsed, selected, disabled, light, dark, empty, ready, waiting, and missing states were visually checked.

## Comparison history

- P1 before: the sidebar could not collapse. Fixed by exposing the toggle and implementing 220 px / 64 px rail states. Post-fix evidence: `ux-audits/test-62-readability/12-support-light-collapsed-final.png`.
- P1 before: light status text could become effectively white on white after a theme change. Fixed by using `SetResourceReference` for semantic status brushes. Post-fix evidence: `ux-audits/test-62-readability/10-flights-light-final.png`.
- P1 before: keyed page titles could retain a light-theme foreground in dark mode. Fixed by explicitly binding `PageTitle.Foreground` to `TextPrimary`. Post-fix evidence: `ux-audits/test-62-readability/09-flights-dark-final.png`.
- P1 before: update/uninstall were under Support and the telemetry log was hidden under diagnostics. Fixed by regrouping actions into Home, Flights, and Support task tabs. Post-fix evidence: final Home, Flights, and Support captures.
- P2 before: raw text fields stayed white in dark mode. Fixed with theme-aware implicit TextBox tokens. Post-fix evidence: `ux-audits/test-62-readability/09-flights-dark-final.png`.
- P2 before: the initial red/green hues felt muddy; an interim violet/amber/blue mapping did not match the final requested semantics. Final fix: contrast-adjusted red/amber/green tokens with a dedicated amber Ready light and 13 px semibold status labels. Post-fix evidence: `ux-audits/test-62-readability/14-flights-light-rag-final.png` and `15-flights-dark-rag-final.png`.

## Findings

No actionable P0, P1, or P2 visual differences remain for the requested Codex-like shell, task-based information architecture, theme readability, semantic colors, or collapsed rail.

## Interaction and build checks

- Release build: passed with 0 warnings and 0 errors.
- Standalone package: passed as `v0.1.0-test.66`.
- Home, Flights, and Support tabs: passed through Windows UI Automation.
- Collapse and expand rail: passed; accessible descriptions update correctly.
- Light and dark themes: passed after live switching.
- Packaged preview: left open on Home for inspection.
- Mutating install, uninstall, GitHub sign-in, recording, and repair operations were intentionally not invoked during visual QA.

## Follow-up polish

- P3: retain the user's preferred rail state between launches if persistent navigation preferences become desirable.

final result: passed
