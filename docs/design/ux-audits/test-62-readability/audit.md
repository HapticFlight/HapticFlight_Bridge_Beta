# Launcher UX audit and remediation

## Evidence

- Before, dark: `01-home-dark.png`, `02-flights-dark.png`, `03-support-dark.png`
- Before, light: `04-support-light.png`, `05-flights-light.png`, `06-home-light.png`
- Rebuilt: `09-flights-dark-final.png`, `10-flights-light-final.png`, `11-support-light-final.png`, `12-support-light-collapsed-final.png`
- Reference comparison: `13-reference-final-comparison.png`

## Findings

- [P1] Theme changes could make status text unreadable.
  - Evidence: the light Flights screen rendered the ready status nearly white on white, and a later dark-theme check rendered the Flights title black on black.
  - Cause: local brush instances survived theme changes while application resources were replaced; the keyed page-title style also lacked an explicit foreground.
  - Fix: status foregrounds now use dynamic resource references, page titles explicitly bind to `TextPrimary`, and editable fields have theme-aware defaults.

- [P1] The requested navigation model was missing.
  - Evidence: the previous rail was permanently expanded and the toggle controls were hidden.
  - Fix: Home, Flights, and Support are now persistent tabs in a 220 px rail that collapses to a 64 px icon rail. Tab names remain available to accessibility tooling and as tooltips.

- [P1] Product functions were grouped by implementation detail rather than user intent.
  - Evidence: install lived on Home and a hidden install page, update and uninstall lived under Support, telemetry ports lived under Settings, and the activity log lived on a hidden diagnostics page.
  - Fix: Home now owns setup/install, appearance, update, and uninstall; Flights owns status, recorder, connection settings, telemetry log, and analyzer; Support contains only guides and troubleshooting.

- [P1] Flights did not show the requested telemetry log.
  - Fix: the live launcher/recorder log is a first-class Flights card immediately above the analyzer.

- [P2] Semantic status colors were muddy and inconsistent.
  - Final fix: follow-up direction standardized the system to red for missing/not installed, amber for waiting and the footer Ready light, and green for detected/installed/healthy. The final high-contrast evidence is `14-flights-light-rag-final.png` and `15-flights-dark-rag-final.png`.

- [P2] Secondary copy and field styling were too weak in both themes.
  - Fix: secondary text contrast and borders were strengthened, key status labels were increased to 13 px semibold, and text inputs now use theme-aware surfaces and foregrounds.

## Interaction checks

- Home, Flights, and Support tabs: passed.
- Expand/collapse rail: passed in both directions.
- Accessible tab names and collapse/expand descriptions: passed.
- Light and dark theme rendering: passed.
- Home update controls present: passed.
- Home uninstall action present and accessible when installed: passed.
- Flights recorder, connection settings, telemetry log, and analyzer present: passed.
- Support guide and troubleshooting paths present: passed.

## Residual test boundary

Installation, uninstall, GitHub authentication, recording, and repair actions were not executed during visual QA because they mutate local or external state. Their controls and existing handlers were retained.
