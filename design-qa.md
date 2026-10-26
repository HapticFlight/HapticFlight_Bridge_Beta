# Reference UI verification — 2026-10-26

The earlier visual-pass claims are superseded. They incorrectly accepted a distorted layout.

## Current preview

`tools/DcsExtraTelemetry.Launcher/.codex-build/bin/latest-preview/DcsExtraTelemetry.Launcher.exe`

## Scaling fix — verified

The old Viewbox used Stretch=Fill and independently scaled X and Y. This flattened
text and turned circular gauges into ellipses in a short window. It has been
replaced with a bounded Grid and one shared scale factor for both axes. The logical
viewport height follows the actual window height, and page content scrolls within
it. Data refresh and navigation no longer reset window geometry. The default size
uses the monitor's actual DPI. The draggable caption was reduced to the title strip
so it no longer intercepts top-row controls.

Visually inspected the default 720 x 769-DIP capture and a 720 x 450-DIP resized
capture. The short window retained circular gauges and ordinary text proportions,
and exposed a vertical scrollbar. Captures are under
`dist/diagnostics/ui-uniform-20261026/`.

## Reference layout work — verified in the application

- Dashboard: compact metrics, three overview cards, wave decoration, setup gauge,
  modelled-intensity/flight-time charts, saved-flight table and activity card.
- Setup: three-step banner, folder/install column and verification/safety/removal column.
- Aircraft: four summary cards and six profiles arranged in three columns.
- Support: diagnostics/location/log column and appearance/ports/updates/removal column.
- Flights: recorder/saved-flight row, analyzer tabs, timeline and lower Replay/effect cards.
- Navy, indigo and violet panel gradients and the blue/violet help-card background.

## Data and interaction checks

Imported the existing CH-47F recording DcsExtraTelemetry-20261026-125803.jsonl.gz.
It rendered 1:52, 3,929 valid samples and 41 modelled effects without a crash.
The screenshot shows 24 active effect rows, whereas the provided example has eight.
The timeline uses a bounded scroll region for the extra rows.

No DCS hook or SimHub plugin was installed or removed during verification.

## Build checks

Release build, Launcher UI contract, Launcher efficiency structural checks and
git diff --check passed. The UI contract now guards equal X/Y scaling and the
bounded viewport instead of requiring the obsolete auto-resize implementation.

## Outstanding fidelity differences

The reference screenshots contain sample recordings, counts, profile versions,
coverage values and a configured setup. The isolated executable has no package
adjacent to it and starts in a different state. These sample values were not
substituted for real installation or recording state. Typography, individual
icons, profile badges, and some card details still differ; a full pixel-level
1:1 claim would be inaccurate. The analyzed recording has different signals and
effect rows from the UH-60 example. The default/resized scaling regression is fixed,
but exact visual parity across all populated states is not yet established.

final result: blocked for full 1:1 visual parity; scaling and navigation verified
