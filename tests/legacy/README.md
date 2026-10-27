# Historical regression checks

These checks reproduce a frozen historical release condition and are excluded
from the current pull-request suite. `Test-RealismBaseline.js` compares the
`realism-test7` inventory to the source profiles that existed at that stage.
Later, intentional profile changes make that comparison fail by design.

Run it only from the matching historical source revision, or after deliberately
regenerating and reviewing that stage's inventory. Do not change its expected
hashes merely to make a current-release pull request pass.
