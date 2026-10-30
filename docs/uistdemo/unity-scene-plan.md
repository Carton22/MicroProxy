# UIST Demo Unity scene

Update only `Assets/Scene/UIST Demo/Full_Display_Microgesture.unity` using an Editor utility scoped to that exact scene. Reuse the approved web catalog, copying all ten original PNGs unchanged into `Assets/Resources/UISTDemo/Items/`.

Replace the 25 proxy children with item1–item10. Bind their marker indices to 0–9 and rebuild Color (Yellow, Black, Green, Red), Topic (Art, CS), and Type (Book, Pen, Box) options from the shared catalog. Keep existing gestures and scene wiring.

Use 108 × 196 card cells with separate caption space. Stretch only the image container within the card; use a FitInParent AspectRatioFitter on each RawImage with that texture's original width/height ratio, full UV rectangle, and uniform transform scale. Leave shared prefabs and other scenes unchanged.

Verification: run the Editor validation on the old scene to confirm failure, apply the catalog, validate exact counts/names/textures/marker assignments/filter memberships and photo ratios, save the scene, inspect the scene view, and confirm compilation has no new errors. Retain an Editor menu to reapply or validate this scene.

Layout correction: reserve the total-price bar at y=-0.115 m and filter bar at y=-0.455 m in their shared head-relative coordinate frame. At the maximum selected-card scale (1.08 × 1.06), the compact cards leave 28.8 mm above and 38.8 mm below. This preserves the original photo FitInParent ratios and all gesture/marker/attribute logic.

Bar polish: filter buttons use 140 × 48 cells, the total-price pill uses 180 × 48, and all bar buttons use uniform scale 1. Their text stretches within padded bounds and auto-sizes for world-space versus canvas text. Clearance is checked with both bars visible and maximum selected-card enlargement.
