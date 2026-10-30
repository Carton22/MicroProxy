# UIST Demo scene verification

PASS: 10 objects, 10 original photos, 0-based markers 0-9, 9 attribute values with exact memberships, full-image UVs, and FitInParent photo ratios.

The saved scene references all ten imported image GUIDs and contains ten FitInParent components with the original PNG width/height ratios. Unity compilation completed without new compiler errors. Visual QA confirmed readable world-space captions and full photos. Headset execution was not performed.

| Object | Type | Color | Topic | Image dimensions |
| --- | --- | --- | --- | --- |
| item1 | Book | Yellow | CS | 300 × 501 |
| item2 | Book | Black | CS | 302 × 501 |
| item3 | Box | Yellow | CS | 310 × 507 |
| item4 | Book | Green | CS | 305 × 505 |
| item5 | Book | Yellow | Art | 300 × 499 |
| item6 | Pen | Black | Art | 302 × 504 |
| item7 | Box | Green | Art | 309 × 498 |
| item8 | Pen | Green | Art | 300 × 495 |
| item9 | Box | Red | Art | 301 × 496 |
| item10 | Pen | Red | Art | 301 × 499 |

Editor menu: Tools → UIST Demo → Validate Active Scene. Apply Approved Catalog rebuilds this scene from Assets/Resources/UISTDemo/catalog.json. The utility refuses to modify other scenes or operate in Play mode.
