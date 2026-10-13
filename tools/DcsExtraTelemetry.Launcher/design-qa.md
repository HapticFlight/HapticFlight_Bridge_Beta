# Launcher glass redesign visual QA

final result: passed

## Complete scaling and stale-UI audit - 2026-08-14

- The final default is 1283 x 893 WPF DIPs, centered and clamped only when the work area is smaller. On this machine's 200% display that is approximately 2566 x 1786 physical pixels, correcting both the earlier tiny 1283-physical-pixel window and the later 94%-wide frame with undersized fixed controls.
- A single 1120 x 780 reference composition is uniformly fitted. Sidebar, type, buttons, cards, and spacing now scale together; the action area no longer expands independently across the monitor.
- Current Release evidence is `.codex-build\home-qa.png`, `.codex-build\flights-qa.png`, `.codex-build\support-qa.png`, the three `*-light-qa.png` page renders, and `home-system-qa.png`.
- Home, Flights, and Support report `ComputedVerticalScrollBarVisibility=Collapsed`. Flights retains the Recent Flights rail and all recorder/open/import/log controls.
- Timeline and selected-effect-strip clicks update the same shared cursor in Timeline and Combined load. The phase intensity key follows the selected category; the final Weapons selection renders red.
- `WindowBackdrop.Apply=True` on Windows build 26200. Release rebuild and both launcher contract checks pass with zero compiler warnings or errors.

## Exact Home reference correction - 2026-08-14

- Source visual truth: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-af4a82aa-d986-49dd-9ea2-6a173c5407a2.png`.
- Structural Release capture: `.codex-build\home-qa.png`, rendered at the launcher's 1120 x 760 WPF viewport.
- Actual-window validation: the Release executable was launched and captured through Windows.Graphics.Capture so the DWM backdrop, ambient light, and translucent panels were judged in their real composited state rather than from a transparent off-screen bitmap.

### Superseding findings and fixes

1. P1: the selected Home item was purple and did not match the reference's neutral gray glass selection. Fixed by using the neutral `NavActiveBackground` token and preserving it while the selected item is hovered.
2. P1: Home still mixed the earlier three-column card composition with the supplied four-step flow. Fixed by matching the reference hierarchy exactly: four connected setup stages, a full-height Environment panel, and stacked Components/Profiles cards.
3. P1: the right Profiles card stopped at its content instead of continuing to the Environment panel baseline. Fixed by changing the right column from a content-sized stack to an Auto/star grid.
4. P1: ambient purple/teal/warm light and card tint were too saturated. Fixed by reducing the ambient alpha and neutralizing the glass materials; the component panel retains only the subtle cool cast shown in the reference.
5. P1: Home's supplied action controls are compact capsules, while unrelated launcher/chart controls are not. Added opt-in Home-reference pill styles and applied them only to Install everything, Repair, Change, component Install, and Open profiles folder.
6. P2: the developer-facing `LOOPBACK` label was unclear. The visible footer now says `LOCAL CONNECTION`, and its tooltip explains that both ports are local to this PC and send nothing over the network. The technical path inside Environment uses the same plain-language term.
7. P2: the setup strip and right cards were taller than the reference. Their vertical padding and row sizing now align with the reference at the same logical viewport.

### Final acceptance

- Home/Flights navigation was exercised in the real Release window; the app returned to Home and was left running for inspection.
- `WindowBackdrop.Apply` returned `True` on Windows build 26200, and the actual-window capture showed the DWM backdrop compositing through the ambient and panel layers.
- The machine is currently in `Setup required`, so its red status and `Three steps left` copy intentionally differ from the reference's configured `Waiting for DCS` / `Two steps left` state.
- Release build: 0 warnings, 0 errors.
- `Test-LauncherUiContract.ps1`: passed.
- `Test-LauncherEfficiency.ps1`: passed.
- `git diff --check`: passed (line-ending notices only).

final result: passed

## 2026-08-13 deep Home fidelity assessment

- Canonical visual source: `C:\Users\Nicho\OneDrive\Pictures\Screenshots 1\Screenshot 2026-08-12 202451.png`.
- Reported pre-fix render: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-af5419ed-30ed-4ca6-8f87-4b706069dbdb.png`.
- Final Release render: `.codex-build\home-current-final.png`.
- Normalized full comparison: `.codex-build\home-audit-final.png`.
- Normalized focused material/card comparison: `.codex-build\home-audit-focused.png`.
- Source app crop: 1255 x 845 pixels. Release capture: 2240 x 1520 pixels at 192 DPI. Both were normalized to 1120 x 760 for like-for-like inspection.

The supplied design was treated as the visual truth rather than as a loose theme reference. The final pass rebuilt the background as three localized ambient fields over the real DWM material, corrected the sidebar and card proportions, tightened the title hierarchy, aligned the setup strip and right-column cards, lowered the bottom action row, and removed mouse focus artifacts. All fills still resolve from `DynamicResource` keys and all glass panels remain translucent.

### Pixel-sampled material check

| Surface | Reference RGB | Final RGB |
|---|---:|---:|
| Window background | 22, 15, 19 | 21, 15, 19 |
| Sidebar | 33, 32, 47 | 34, 32, 47 |
| Selected navigation | 87, 80, 133 | 87, 79, 132 |
| Idle setup segment | 31, 41, 54 | 32, 39, 52 |
| Active profile card | 32, 37, 47 | 33, 37, 47 |
| Active profile row | 45, 48, 58 | 46, 48, 59 |
| Cool component card | 26, 60, 72 | 24, 62, 75 |
| Updates card | 26, 46, 57 | 23, 46, 58 |
| Warm lower glow | 33, 20, 22 | 33, 19, 25 |

No sampled material differs by more than three RGB values per channel. The upper-left purple, upper-right teal, and lower warm glow are now spatially localized like the supplied composition instead of appearing as a broad flat gradient or black fill.

### Findings and disposition

1. P1: the prior window material read as black and lost the three-direction ambient glow. Fixed with palette-driven radial layers over the DWM backdrop.
2. P1: glass surfaces were too dark and visually collapsed into the background. Fixed by matching panel alpha/tint values to sampled reference regions.
3. P2: the sidebar was too narrow internally because content had duplicate inset. Fixed by restoring the 240-DIP rail and a single padding layer.
4. P2: the title, setup strip, and cards were vertically compressed compared with the source. Fixed by matching the reference rhythm and restoring the bottom-action breathing room.
5. P2: the right-column cards were under-padded. Fixed with consistent 18-DIP card insets and compact 10-DIP component rows.
6. P1: ordinary actions could inherit exaggerated pill/oval geometry or retain a blue mouse-focus halo. Fixed with a dedicated 8-DIP rounded-rectangle action template and keyboard-only focus visual.
7. P2: selected navigation lost its lavender surface on hover. Fixed by retaining the selected material through hover state.
8. P1: light mode did not remain readable over a dark desktop. Fixed by increasing the light fallback material opacity while retaining translucency.

The only deliberate geometry difference from the original mock is that ordinary action buttons remain compact rounded rectangles instead of pills. This follows the user's repeated explicit rejection of oval action buttons. Semantic status and segmented selection controls keep their separate state-indicator geometry.

### Live interaction and platform verification

- Home, Flights, and Support navigation were exercised in the real Release executable.
- Light, Dark, and System appearance modes were switched live; System was restored before handoff.
- The Flights page reported no visible page scrollbar at the tested 1120 x 760 viewport.
- `WindowBackdrop.Apply` returned `True` on Windows build 26200, and the backdrop visibly composited beneath the deterministic ambient layers.
- Dynamic setup text differs from the reference only because this machine is currently in the `Setup required` state rather than the reference's configured `Waiting for DCS` state.
- No actionable P0, P1, or P2 Home visual mismatch remains in the normalized comparison.

The analyzer was rebuilt and revalidated against the new exact reference supplied on 2026-08-13. The 11-pixel document strip above the source app and 16-pixel strip below it are not part of the UI; the implementation was rendered at the reference app region's exact 1279 x 889 viewport. The resulting comparison aligns the title bar, tab bar, timeline toolbar, card bounds, plot origin, phase band, 15-row density, single bottom axis, and hover inspector.

The latest validation uses the actual 25:09 UH-60L DAP recording from the reported failure, not synthetic chart data. Its 41 effects and 17 raw detector spans reproduce the original failure state. The presentation layer consolidates the raw spans into 10 chronological sections without changing the analyzer result, native chart tooltips are disabled in favor of the dedicated bottom inspector, and effect names/frequencies use separate columns so the frequency never overlaps a trimmed name.

## Comparison target

- Source visual truth: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-4021e64a-b091-4e3e-867a-5acd1794a428.png`
- Implementation capture: `tools\DcsExtraTelemetry.Launcher\.codex-build\home-qa.png`
- Combined comparison: `tools\DcsExtraTelemetry.Launcher\.codex-build\home-comparison-new.png`
- Focused button comparison: `tools\DcsExtraTelemetry.Launcher\.codex-build\home-buttons-comparison.png`
- Viewport: 1120 x 760 WPF device-independent pixels, dark theme, Home page.
- Source pixels: 1986 x 1492. The 88 px document heading was removed before comparison.
- Implementation pixels: 2240 x 1520 at 192 DPI. Both app regions were normalized to 900 x 610 for the combined comparison.

## Required fidelity surfaces

- Fonts and typography: Segoe UI Variable Text and Cascadia Mono retain the supplied implementation package's Windows-native substitutions. Title, section, body, and mono hierarchy match the reference.
- Spacing and layout rhythm: the floating sidebar, three-stage strip, Active profile card, Installed components card, Updates card, and bottom actions now occupy the same hierarchy and vertical sequence as the reference. Standard actions use compact 10-DIP rounded rectangles; only semantic status chips and segmented selectors retain pill geometry.
- Colors and visual tokens: dark purple, black, teal, and warm ambient layers are sourced from DynamicResource palette keys. Cards are translucent dark tints with subtle hairline borders instead of opaque or brightly outlined surfaces.
- Image and icon fidelity: the packaged HapticFlight logo and Windows Fluent/MDL2 navigation icons are used. No placeholder graphics are present.
- Copy and content: Home copy and visible card labels match the reference. Status values remain live; `Setup required` replaces the reference's `Waiting for DCS` only when the machine is not configured.
- State wiring: the visible Home controls now own the preserved x:Names and handlers directly. They no longer mirror values from a separate collapsed page, so installation badges, status lights, step states, profile state, and update text render on the real composition.

## Full-view comparison evidence

The combined comparison verifies the same Home hierarchy and dark state. The source screenshot is cropped at its right edge while the implementation capture includes the complete window; this is a capture difference, not responsive drift.

## Focused region evidence

- Sidebar/nav: selected Home row is a filled glass capsule without the previous purple focus outline; footer uses the two-line loopback/ports readout.
- Buttons: the focused comparison verifies that Install, Change, update, sign-in, and bottom actions no longer use the previous 999-DIP fully oval template. They share a 10-DIP rounded-rectangle template with consistent insets and heights. The status chip remains a pill because it is a state indicator, not an action button.
- Home cards: the middle setup stage is selected, Active profile uses one highlighted status row, component rows use compact Install actions, and installed states retain their check icon badges.

## Comparison history

1. P1: the implementation used a four-stage chain, Environment/Profiles cards, extra rows, and top actions. Fixed by rebuilding the Home hierarchy from the selected screenshot.
2. P1: card and control borders were far brighter than the source and panel fills were too opaque. Fixed by restoring subtle glass alpha values and dark tinted surfaces from the implementation package.
3. P1: Installed components and Updates started beside Active profile and pushed the left card downward. Fixed by giving the left and right compositions independent vertical flows.
4. P2: sidebar navigation had double horizontal inset and mouse focus produced an outline absent from the source. Fixed by removing the duplicate inset and using the selected fill as the resting state.
5. P2: setup labels overflowed the stage strip. Fixed with the source-height strip, clipped bounds, compact padding, and fitted type.
6. P1: all ordinary actions inherited `CornerRadius="999"`, producing exaggerated oval ends throughout Home and the other pages. Fixed at the shared template with 10-DIP action corners while retaining separate pill templates only for status and segmented controls.
7. P1: Active profile was positioned over the setup strip with a row-span and margin offset, allowing the cards to touch or overlap at runtime. Fixed by replacing the overlay with a normal three-row left composition: setup strip, 14-DIP gap, then Active profile.

## Flight analyzer chart QA

- Source visual truth:
  - Exact timeline composition: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-1bb40617-35fb-4c39-a819-7454cb1e1fb1.png`
  - Timeline: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-14be0203-b23b-42f4-b606-ff869fae74c8.png`
  - Flight phases: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-ad1be3f3-8a7f-481a-968a-9c76e7243364.png`
  - Combined load: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-57626a78-4c33-469c-81ad-9bb57e5fb132.png`
- Implementation captures: `.codex-build\charts-timeline-qa.png`, `.codex-build\charts-phases-qa.png`, and `.codex-build\charts-load-qa.png`.
- Exact same-viewport timeline comparison: `.codex-build\charts-timeline-exact-comparison.png` (reference app region on the left, implementation on the right).
- Combined comparisons: `.codex-build\charts-timeline-comparison-new.png`, `.codex-build\charts-phases-comparison-new.png`, and `.codex-build\charts-load-comparison-new.png`.
- Viewport and density: the final timeline comparison uses 1279 x 889 at 96 DPI, matching the reference's actual app region pixel-for-pixel. Phase and Combined load use the same live window composition and responsive card grid.
- State: dark theme, synthetic UH-60L DAP recording with 39 effects and 8 detected phases. The analyzer's new focused view selects 15 balanced effects across the four categories, matching the reference count while keeping all effects available.

### Chart fidelity surfaces

- Fonts and typography: UI labels use Segoe UI Variable Text with single-line trimming; numeric summaries, axes, phase headers, and readouts use Cascadia Mono/Consolas. Long effect and category labels no longer wrap into adjacent rows.
- Spacing and layout rhythm: the analyzer now follows the reference hierarchy of title bar, tab/toolbar row, one flexing chart card, and a bottom hover strip. Combined load restores the two-card dashboard row, selected-effect strip, and gain summary.
- Colors and visual tokens: chart lanes, phase bands, grid lines, cursor, categories, and text all resolve through DynamicResource-backed chart keys. No chart paints its own opaque background.
- Image and icon fidelity: the packaged HapticFlight asset is retained. The charts are native data visualizations rather than substituted raster imagery.
- Copy and content: the title bar carries the effect/phase summary, Timeline exposes Hide unused, Phase exposes the intensity ramp and category legend, and Combined load includes peak concurrency, category peaks, selected effect, and suggested gain.
- Interaction and function: the Effects chooser applies focused, active-only, all, or per-effect selections across Timeline, Flight phases, and Combined load. Chart clicks share selected effect/time state, the cursor propagates between views, and Combined load exports a real CSV effect report.

### Chart comparison history

1. P1: the selected chart tab displayed a large lavender oval focus ring because every pill button used a 999-DIP corner radius. Fixed by moving all interactive pill buttons to the same 10-DIP rounded-rectangle geometry as the standard actions; non-interactive status chips remain separate.
2. P1: Timeline omitted Hide unused, duplicated the effect count inside the card, and used full category names inconsistent with the reference. Fixed by restoring the toolbar action, moving the count to the title bar, and using the compact Rotor label.
3. P1: Phase kept the intensity ramp inside the card, omitted the category legend, wrapped long labels, and omitted frequencies. Fixed by moving the ramp to the toolbar, restoring the legend, enforcing one-line chart labels, and rendering frequencies beside effect names.
4. P1: Combined load lacked the selected-effect strip, suggested gain block, category markers, and visible peak-concurrency annotation. Fixed using the existing analysis result and existing gain formula without changing recording or analysis computation.
5. P2: Combined load showed five time labels and collided with the center concurrency annotation. Fixed by retaining only the reference's start/end labels and using the center slot for peak concurrency.
6. P1: the implementation still drew the complete 41-effect catalog while the reference intentionally showed a focused 15-effect analysis. Fixed with a real shared effect-selection model and a balanced 15-effect default; this is user-adjustable rather than hardcoded chart data.
7. P1: choosing another recent recording while an analyzer window was already open reactivated the stale analyzer. Fixed by reusing the window only for the same recording and rebuilding it for a different selected recording.
8. P1: native WPF tooltips duplicated the bottom inspector and floated over chart bars with overlapping glyphs. Fixed by removing native chart tooltip assignment from Timeline, Flight phases, and Combined load; hover now updates only the dedicated inspector and clears on mouse leave.
9. P1: the reported 25:09 recording produced 17 raw phase spans, including adjacent duplicates and short chronological regressions, which rendered as a row of phase capsules. Fixed with a shared presentation-only phase timeline used by all three charts and window counters. The analyzer's original spans remain unchanged; the reported recording presents 10 chronological sections.
10. P1: long effect labels consumed the frequency text and selected effects dimmed every other row. Fixed by reserving a separate aligned frequency column and using a subtle selected-row highlight without suppressing the remaining flight context.

### Intentional product constraints

- `Open recordings` remains in the analyzer title bar because it preserves the existing easy return path requested for recent recordings.
- `Export effect report` is now functional and writes a CSV containing the recording summary, every effect, category, frequency, peak, event count, and whether it is in the current view.
- The recorder does not persist a packet-drop counter, so the requested `Dropped` summary row is present but explicitly reads `Not tracked`; no value is invented from timing gaps or invalid lines.
- No actionable P0/P1/P2 chart mismatch remains after the final three side-by-side comparisons.

## Verification

- Release build passed with no compiler warnings.
- `Test-LauncherUiContract.ps1` passed.
- `Test-LauncherEfficiency.ps1` passed.
- `git diff --check` passed; only line-ending conversion notices were emitted.
- `WindowBackdrop.Apply` returned `True` on Windows build 26200.
- Actual-recording captures: `.codex-build\charts-actual-timeline-qa.png`, `.codex-build\charts-actual-phases-qa.png`, and `.codex-build\charts-actual-load-qa.png`.
- Actual-recording comparisons: `.codex-build\charts-actual-timeline-comparison.png`, `.codex-build\charts-actual-phases-comparison.png`, and `.codex-build\charts-actual-load-comparison.png`.

## Timeline rail and ambient-glow correction

- Reported runtime source: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-6ff7e59b-0733-42ba-b159-e0118669f248.png`.
- Current Release timeline render: `.codex-build\charts-timeline-qa.png` at 1279 x 889.
- Same-input comparison: `C:\Users\Nicho\.codex\visualizations\2026\08\13\019ff897-ad65-7113-88a5-8b991fca8961\timeline-rail-glow-comparison.png`.
- Replaced the disabled three-pixel WPF scrollbar with a chart-native continuous overview rail. Its viewport thumb, cursor, click-to-pan, and drag-to-pan behavior are driven by the same view state as the timeline chart.
- Rebuilt the phase header as one continuous band with meaningful dividers instead of alternating filled fragments.
- Restored deterministic purple, teal, and warm ambient layers above the DWM backdrop and lowered only the analyzer-card opacity enough for those layers to transmit through the glass.
- `WindowBackdrop.Apply` returned `True`; the deterministic ambient layers also remain visible on the Windows 10 solid-fill fallback.
- Release build, launcher UI contract, launcher efficiency checks, and `git diff --check` passed. No compiler warning was emitted.

## Selected-category legend and timeline cursor correction

- The Flight phases intensity ramp now resolves all three swatches from the currently selected effect category: Airframe purple, Rotor amber, Ground green, and Weapons coral/red. The selected Weapons verification render is `.codex-build\charts-phases-selected-weapons-qa.png`.
- Clicking inside any Timeline effect lane now updates both the selected effect and the shared time cursor. A 78% click verification moved the cursor to 886.86 seconds of the 18:57 QA recording; the render is `.codex-build\charts-timeline-click-78-qa.png`.
- The Timeline cursor is a single 1.25-DIP line. The previous five-DIP glow block was removed to match the thin reference cursor while retaining the shared cursor position on the overview rail.
- Side-by-side evidence: `C:\Users\Nicho\.codex\visualizations\2026\08\13\019ff897-ad65-7113-88a5-8b991fca8961\phase-intensity-selected-weapons-comparison.png` and `C:\Users\Nicho\.codex\visualizations\2026\08\13\019ff897-ad65-7113-88a5-8b991fca8961\timeline-thin-cursor-comparison.png`.

## Clickable selected-effect timeline and Flights layout correction

- Source visual truth: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-27b079c5-6ed3-4169-9c9f-ee7f7b1c9bd8.png` and the reported broken Flights capture `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-5803387a-4d53-42a7-9f97-5aee80fee54b.png`.
- Implementation captures: `.codex-build\charts-load-qa.png` and `.codex-build\flights-qa.png` at 1279 x 889 and 1120 x 760 device-independent pixels respectively.
- Full-view comparison: `C:\Users\Nicho\.codex\visualizations\2026\08\13\019ff897-ad65-7113-88a5-8b991fca8961\flights-layout-fix-comparison.png`. Focused interaction comparison: `C:\Users\Nicho\.codex\visualizations\2026\08\13\019ff897-ad65-7113-88a5-8b991fca8961\selected-effect-click-comparison.png`.
- P0 fixed: the Flight recorder's `Auto` action column could consume the card width and collapse its copy to one character per line. The card now reserves a 317.6-DIP copy column and a 476.4-DIP action region at the reported viewport.
- P1 fixed: the page-level vertical scrollbar was visible in the empty Flights state. The corrected render reports `ComputedVerticalScrollBarVisibility=Collapsed`, while wheel/programmatic scrolling remains available for smaller windows.
- P1 fixed: the selected-effect activity strip had no click behavior. Clicking it at 72% now sets the shared cursor to 818.64 seconds; both Timeline and Combined load report the same cursor value and the strip redraws its own thin cursor at that position.
- Typography and layout rhythm: no one-character wrapping, clipping, control overlap, or hidden action remains. Ordinary action controls use 8-DIP rounded rectangles; status pills and segmented tabs retain their intentionally separate geometry.
- Colors and image assets: DynamicResource glass/chart tokens and the existing packaged logo/icon assets are unchanged. No raster substitute, hardcoded chart color, or opaque overlay was introduced.
- Copy/content: recorder actions, recent-recording access, connection details, recording guidance, handlers, and automation metadata remain present.

## Title status geometry correction

- Source visual truth: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-21e3b951-f72c-44b8-9056-3e44563e8478.png`.
- Implementation capture: `.codex-build\flights-qa.png`, 2240 x 1520 pixels at 192 DPI for a 1120 x 760 WPF viewport.
- Focused same-position comparison: `C:\Users\Nicho\.codex\visualizations\2026\08\13\019ff897-ad65-7113-88a5-8b991fca8961\title-status-button-fix-comparison.png`.
- P1 fixed: the title status inherited the `TelemetryPill` 999-DIP corner radius and rendered as a horizontal ellipse. The title instance now overrides that template with an 8-DIP radius, matching the ordinary rounded-rectangle action geometry while preserving the live status text, semantic dot color, glow, and title-bar hit testing.
- Typography, spacing, colors, imagery, and copy are unchanged in the normalized crop. The only visible change is the requested control geometry; no adjacent layout or behavior was redesigned.

## DPI-normalized launcher sizing correction

- Source visual truth: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-af4a82aa-d986-49dd-9ea2-6a173c5407a2.png`, 1283 x 893 pixels.
- Reported oversized runtime capture: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-d5f9fa5f-51f5-47eb-ac1a-fa00b8998a0d.png`, 2234 x 1511 pixels.
- Corrected Release implementation capture: `.codex-build\home-qa.png`, 1283 x 893 pixels at 192 DPI from a 641.5 x 446.5 DIP viewport.
- Exact-density side-by-side comparison: `C:\Users\Nicho\.codex\visualizations\2026\08\13\019ff897-ad65-7113-88a5-8b991fca8961\home-sizing-comparison-exact.png`.
- P1 fixed: the 1120 x 760 DIP launcher was rendered directly on a 200% display, producing a roughly 2234 x 1511 physical-pixel window. The launcher now keeps a fixed 1120 x 780 reference canvas inside one root Viewbox and derives its initial DIP window dimensions from the active monitor DPI, producing the reference's 1283 x 893 physical-pixel footprint.
- P1 fixed: shrinking only the window previously forced normal grid reflow, label collisions, and sidebar overlap. The reference canvas now scales as one composition, preserving the source's sidebar width, header rhythm, stage strip, card geometry, buttons, and typography without per-control overrides.
- The comparison uses the same 1283 x 893 output size on both sides. The visible `Two steps left` versus `Three steps left`, green profile state versus unconfigured profile state, and yellow versus red title status are live machine-state differences, not geometry drift.
- Release build passed with zero compiler warnings and zero errors. Launcher UI contract, launcher efficiency checks, and `git diff --check` passed. `WindowBackdrop.Apply` returned `True` on Windows build 26200.
- Final result: passed. No actionable P0, P1, or P2 sizing mismatch remains in the same-density comparison.

## Fixed-density resize and maximize correction

- Reported normal-window regression: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-003326b8-dec8-423c-833a-4676e7c3e3d7.png`; the right action area was cropped.
- Reported maximized regressions: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-273a2167-3e56-4683-ae8d-bc390329464b.png` and `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-c00b574f-ae0e-48b2-8b3e-1cdd63ac3a3d.png`; the entire reference canvas was enlarged with the window.
- Corrected normal capture: `.codex-build\home-normal-fixed-density.png`, 1283 x 893 pixels. Corrected maximized captures: `.codex-build\home-max-fixed-density.png` and `.codex-build\flights-max-fixed-density.png`, 2880 x 1920 pixels.
- Same-viewport comparisons: `C:\Users\Nicho\.codex\visualizations\2026\08\13\019ff897-ad65-7113-88a5-8b991fca8961\home-fixed-density-comparison.png` and `C:\Users\Nicho\.codex\visualizations\2026\08\13\019ff897-ad65-7113-88a5-8b991fca8961\home-maximize-regression-comparison.png`.
- P0 fixed: resize and maximize no longer use the outer window dimensions as a zoom factor. The composition holds its monitor-normalized physical density and gives the WPF layout additional logical width and height instead, so text, buttons, cards, navigation, and icons do not become giant.
- P1 fixed: non-reference window aspect ratios no longer crop the right edge. The reference surface is sized to the actual viewport at one uniform composition scale, preserving the complete title actions and right column.
- P1 fixed: the maximize/restore button no longer renders the lavender keyboard-focus outline. The button remains clickable, retains its name and handler, and still changes between maximize and restore glyphs.
- P1 fixed: Home and Flights now consume additional maximized width through their existing responsive grids; Flights retains recent-flight navigation and does not expose a page scrollbar in its empty state.
- Release build passed with zero compiler warnings and zero errors. Launcher UI contract and efficiency checks passed. `WindowBackdrop.Apply` returned `True` on Windows build 26200.

## Live-width right-edge correction

- Reported live capture: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-c385411f-f137-427a-ac54-28a62e2d8969.png`, 1280 x 907 pixels. The live window showed the Home action group and right column running into the viewport edge even though the earlier offscreen render had passed.
- Final implementation captures: `.codex-build\home-live-width-gutter-qa.png` and `.codex-build\flights-live-width-gutter-qa.png`, rendered at the reported 1280-pixel width.
- Target comparison: `C:\Users\Nicho\.codex\visualizations\2026\08\13\019ff897-ad65-7113-88a5-8b991fca8961\home-right-gutter-target-comparison.png`. Before/after comparison: `C:\Users\Nicho\.codex\visualizations\2026\08\13\019ff897-ad65-7113-88a5-8b991fca8961\home-right-gutter-before-after.png`.
- P1 fixed: the shared page surface now reserves a four-DIP right gutter, keeping Home and Flights inside the live client width.
- P1 fixed: Home's right column is 317 DIPs, reproducing the supplied target's component-card width while reducing the oversized Environment card by the same amount.
- P1 fixed: the Home title actions have their own measured inset and the primary action width matches the target. `Install everything` and `Repair` now render completely without shifting the title or window controls.
- The final Release build has zero warnings and zero errors. Both launcher checks pass, the Flights page scrollbar remains collapsed, and `WindowBackdrop.Apply` returns `True` on Windows build 26200.
- Final result: passed.

## Default footprint, page gutter, and ambient-glow correction

- Reported runtime source: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-57b02a14-5273-415a-99ff-3cd33bf2e8dd.png`. The launcher occupied only about 57% of the 2250-pixel desktop width, the shared page surface still appeared flush with the right frame, and the ambient color fields were substantially weaker than the supplied glass reference.
- Corrected Home and Flights captures: `.codex-build\home-default-1600-glow-qa.png` and `.codex-build\flights-default-1600-glow-qa.png`, both rendered at the new 1600 x 1050 physical-pixel default.
- Before/after evidence: `C:\Users\Nicho\.codex\visualizations\2026\08\13\019ff897-ad65-7113-88a5-8b991fca8961\home-default-size-glow-comparison.png`.
- P1 fixed: the previous 1283 x 893 physical-pixel lock reproduced the reference bitmap dimensions but made the launcher visibly too small on this machine's 2250 x 1495 desktop. The default is now 1600 x 1050 physical pixels, centered and clamped to the current work area with a 32-pixel safety inset. Fixed-density resizing remains in place, so controls and type do not become oversized.
- P1 fixed: the shared `ActionArea` now reserves an 18-DIP right gutter. Home and Flights cards end inside the client area instead of meeting the window frame; the same shared gutter applies to the remaining launcher pages.
- P1 fixed: dark-theme purple, teal, and warm ambient colors now use stronger alpha values in `GlassThemePalette`. The gradients remain DynamicResource-driven and semi-transparent, so DWM composition remains visible and the Windows 10 fallback continues to use the same intended color fields.
- Release rebuild passed with zero warnings and zero errors. `Test-LauncherUiContract.ps1` and `Test-LauncherEfficiency.ps1` passed. The Flights page reports `ComputedVerticalScrollBarVisibility=Collapsed`. `WindowBackdrop.Apply` returned `True` on Windows build 26200.
- Final result: passed.

## Work-area-relative default sizing correction

- Reported runtime source: `C:\Users\Nicho\AppData\Local\Temp\codex-clipboard-d9dbb0ec-f11d-4eaf-b548-84fa0d407438.png`, 2325 x 1550 pixels. The prior launcher did open at its requested 1600 x 1050 physical pixels, but that occupied only 68.8% of this desktop width and was still visibly undersized.
- Corrected Home and Flights captures: `.codex-build\home-default-94pct-qa.png` and `.codex-build\flights-default-94pct-qa.png`, rendered at approximately 2186 x 1457 physical pixels for the reported work area.
- Same-desktop footprint comparison: `C:\Users\Nicho\.codex\visualizations\2026\08\13\019ff897-ad65-7113-88a5-8b991fca8961\home-default-94pct-comparison.png`.
- P1 fixed: the fixed 1600 x 1050 default cap has been removed. Initial window dimensions are now 94% of the active Windows work area in each axis, preserving a small visible desktop margin instead of opening at the previous small fixed footprint.
- Fixed-density composition remains active. Enlarging the outer window gives the actual page more room without reproducing the earlier giant-type, giant-button, or cropped-right-column regressions.
- The 18-DIP shared right gutter and strengthened DynamicResource ambient colors remain unchanged. Home and Flights were rendered at the new large default with no clipping, page scrollbar, one-character wrapping, or right-edge collision.
- Release rebuild passed with zero warnings and zero errors. Launcher UI contract and efficiency checks passed. `WindowBackdrop.Apply` returned `True` on Windows build 26200.
- Final result: passed.
