# Regression register — intended differences versus the baseline build

This register lists the corrections and intentional behaviour changes that are allowed to change the result of
the regression scenario compared with the baseline build. The regression harness
(`tools/RegressionDump/run-regression.ps1`) reads the numbers in the first column of the table below; every
file in `tools/RegressionDump/expected-diffs/{polyline,block}` must refer to one of them with a `# bug:` line.
Any other difference in the regression run is a blocker.

Descriptions are short technical summaries.

| Nr | Correction | Stage | Status |
|---|---|---|---|
| 1 | Bar edit command applied a length change twice (edit watcher triggered after rebuilding the bar's companions) | W1 | done — expected-diffs 01 |
| 2 | Erasing a bar without a label left its distributions and annotations in the drawing | W1 | done — expected-diffs 02 |
| 4 | Bar rebuild always produced a horizontal bar (rotation and mirroring were lost) | W1 | done — expected-diffs 04 |
| 5 | Shape 44: inner circle deleted and no rebuild after a change | W1 | done — expected-diffs 05 |
| 7 | Total length not recalculated after changing dimension A (update command, stretch) | W1 | done — expected-diffs 07 |
| 3 | COPY: copied distributions and copied bars kept links to the originals | W2 | done — clone map and re-linking; expected-diffs 03 |
| 3b | ARRAY / multiple COPY: distribution ↔ annotation pairs taken from the clone map | W2 | done — expected-diffs 03b |
| 6 | COPY: a copied bar could take over the label of another bar | W2 | done — two-way pairing only; expected-diffs 06 |
| 9 | Transparent command during a distribution grip could store an intermediate state | W3 | open |
| 10 | Bar end symbol command on a polyline bar showed a misleading message | K3 | done |
| 8 | BS 8666:2020 shapes: outdated tests and formulas updated, shapes 44 and 75, geometry of shapes 12, 22 and 24 | W4 | done — expected-diffs 08 |
| 13 | BS 8666 outer dimensions converted to the bar axis; bend arcs on the axis | W4 | done — expected-diffs 13 |
| 11 | Empty exception handlers replaced by error logging | K2 | done |
| 12 | Grip shortening a straight bar below its minimum length: change reverted consistently | K6 | done — expected-diffs 12 |
| 3c | COPY of a slab with distributions: slab tag of the copies pointed to the original outline | K5 | done — expected-diffs 03c |
| 14 | Diagonal corner bars at re-entrant slab corners and at openings (new requirement) | K7 | done — expected-diffs 14 |
| 16 | Deleting a copied distribution without its own annotation removed the original annotation; UNDO of a bar deletion triggered the cascade | K3 | done |
| 17 | Cutting length of shapes 27 and 28 corrected to the BS 8666:2020 formula | W5 | done |
| 18 | Rounding up to 25 mm without a floating-point tolerance could add one rounding step | W5 | done |
| 19 | Bar counter: mass calculated from dimension A instead of the cutting length | W5 | done |
| 20 | Negative, zero or undefined lengths could reach the bar bending schedule | W5 | done |
| 21 | Shape 12 with zero radius: message and length calculation made consistent | W5 | done |
| 22 | Slabs with a different label prefix were not recognised in the GA import and in the punching report | B | done |
| 23 | Automatic closing of open nib lines in the GA drawing (new function) | A | done |
| 24 | Map import: slab label read from merged MText paragraphs | — | done |
| 25 | Outline orthogonality: tolerance for small deviations of nearly axis-aligned edges | — | done |
| 26 | Bar counter read only the legacy bar records | — | done |
| 28 | Additional reinforcement: a map could be matched to a slab with a similar label prefix | — | done |
| 27 | Additional reinforcement: entity read after the transaction was committed (regression) | — | done |
| 29 | Wrapped nib / nib ending inside an outer edge: bars along the step edge | K1 | done — expected-diffs 29 |
| 30 | Slab mesh next to beam zones (new requirement) | K7 | done — expected-diffs 30 |
| 31 | Sections: straight bars ending in the section drawn with an end symbol (new requirement) | K6 | done |
| 32 | Sections: bar end and lap representation rules Z1–Z6 (new requirement) | Z1–Z6 | done |

Expected-diffs: `tools/RegressionDump/expected-diffs/{polyline,block}/NN-*.txt` (`# bug:` = number from the table).
