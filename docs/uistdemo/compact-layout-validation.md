# Compact UIST layout

Cards: 108 × 196, reduced from 150 × 278. Original photo aspect fitters and caption layouts remain enabled.

Total-price bar: 180 × 48, centered above the carousel at y=-0.115 m. Filter pills: 140 × 48, centered below at y=-0.455 m. Pill backgrounds now contain their padded, automatically sized text.

At maximum selected-card enlargement (1.08 × 1.06), clearance is 28.8 mm above and 38.8 mm below in the shared head-relative plane (equivalent to 28.8 and 38.8 units in the 600-unit UI). This checks the state where total and filters are visible together.

Three regression checks passed: worst-case vertical clearance; preservation of all unrelated scene components, including native microgesture and spatial-anchor wiring; and preservation of the reference/touchpad scenes and all ten photo aspect fitters. Unity's ten-object/attribute/photo-ratio validator also passed.

Build succeeded in 36 seconds. APK: Builds/uistdemo_microgesture_compact.apk.
