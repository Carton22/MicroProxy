# Spatial links and pinch placement

The UIST microgesture scene enables `ProxyLabelManager.m_retainProxyFocusWhenUnfocused`. The manager remembers the proxy by Transform identity while the EventSystem focuses a filter. If filtering hides that proxy, it chooses a visible target; empty results have no link. Filter input focus stays on the filter button. The setting defaults to false for other scenes.

`PinchTargetSpawner` now resolves thumb and middle tips using the skeleton's convention: OpenXR `XRHand_*Tip` IDs for XR hands, legacy `Hand_*Tip` IDs for legacy hands. These enums overlap numerically but refer to different joints. Tip caches reset when the skeleton or convention changes. Midpoint placement requires both fingertips and skips placement if either is unavailable.

The marker prefab, spatial-anchor controls and persistence, microgesture wiring, object catalog, and compact layout are unchanged. Existing saved marker positions are not migrated; test a fresh placement before loading a saved layout (loading locks placement).

Validation: Tools → UIST Demo → Run Interaction Regression Checks. All four checks passed after reproducing three failures before the fix: filter-focus link retention with visibility changes and focus preservation; OpenXR tip resolution; legacy tip resolution; and missing-thumb handling. Checks use isolated Unity objects in a temporary additive scene and restore the original scene afterward. They do not measure live hand-tracking accuracy.

The ten-object/attribute/photo-aspect validator passed. Layout checks passed with 28.8 mm header clearance and 38.8 mm filter clearance at maximum selected-card scale. Scene comparison permits only the intended retention flag in addition to the already-approved compact layout.

Android build succeeded in 67 seconds. APK: `Builds/uistdemo_microgesture_links_pinch.apk`. Quest 3 installation time was 2026-10-30 15:57:53; `com.carton22.uistdemo` was running as PID 21246 and its Unity activity was resumed. Startup logs still contain the existing camera-sample `CameraToWorldCameraCanvas.Start` null reference and SDK GameManager messages; no input or marker exception was reported in this startup check.

## Hide the hand-following anchor preview after load

`HandAnchorPreviewHider` is attached only in the UIST microgesture scene and receives the Meta core's `OnAnchorsLoadCompleted` event alongside the existing marker-restore listener. A callback with at least one valid anchor disables the SDK's placement-preview clone under `RightControllerAnchor`. The original anchor template, loaded anchors, unrelated hand children, marker objects, spatial links, and save/load controls remain active. Null or empty load results leave the preview available for placement.

The interaction suite now has five passing checks. The new check covers failed/empty loads, successful loads, repeated loads, and preservation of restored anchor/marker objects and unrelated hand objects. Scene comparison confirms only the new listener and its component were added. The catalog and image-ratio validator also passed.

The updated Android build succeeded in 55 seconds and was installed on Quest 3 at 2026-10-30 16:12:10 with saved app data preserved; the app was running as PID 24176. APK: `Builds/uistdemo_anchor_preview_hidden.apk`. Live headset anchor localization and the visible result still require a real saved-anchor load by the wearer.

## Proxy cards disappearing on return scroll

The user reproduced the blank-card state twice on Quest 3 while temporary diagnostics sampled the live scene. Items 1–3 were active and within the camera view; their CanvasGroups had nonzero opacity, image and caption components were enabled, and background/photo/mask renderers retained valid four-vertex meshes and materials. Inherited opacity and caption renderer visibility were also valid. This evidence did not support a stuck-fade or missing-geometry explanation.

The live camera-image canvas and proxy canvas both used sorting order 0. Transparent world-space canvases at the same priority can reorder as their bounds move, allowing the opaque camera feed to cover the cards while its marker overlays remain visible. The UIST scene now overrides the camera-image canvas sorting order to -100, behind the proxy UI and its world-space captions. This is a scene-local prefab override; the shared prefab, carousel animation, markers, and input scripts are unchanged.

Seven Unity checks pass, including an actual-card-prefab return-scroll test and an explicit camera-background/caption-order check. The ordering check failed before the scene override. Temporary device tracing was removed before the updated build. The Android build succeeded in 66 seconds and was installed on Quest 3 at 2026-10-30 16:51:47. APK: `Builds/uistdemo_carousel_visibility.apk`. Headset visual confirmation is pending wearer testing.

## Attribute selection and marker occlusion

In the UIST microgesture scene, `CanvasRayIntersectionVisualizer.m_linkAllFilteredMarkers` enables links and selected-circle styling for every marker in the active visibility filter while `AttributeUI` is open. Color = Yellow selects items 1, 3, and 5; combining Yellow with CS selects items 1 and 3. Link eligibility checks exact marker membership, including grouped labels, and honors the existing AND filter intersection. Empty results have no links. Closing the filter bars restores links only to the currently focused proxy while preserving the chosen criteria; reopening them restores all matching links. Clearing filters also restores focused-proxy behavior without moving input focus away from filter controls. Other scenes retain the default single-proxy behavior.

The scene now includes `WorldMarkerVisualHider` with finite-rectangle canvas occlusion enabled. It casts the viewer-to-marker segment against the current passthrough canvas plane and hides both the cube and number visuals when the segment reaches the canvas rectangle first. Markers in front of the canvas, or projected outside its rectangle, remain visible; the check updates as the head/canvas moves. Only Renderer, Canvas, and Graphic visibility changes. Marker GameObjects, bindings, anchor-relative transforms, colliders, and projection/link targets stay intact. Explicit hide-all retains priority. The existing persistence hook remains unassigned so loading alone does not hide every marker.

All ten interaction checks passed. The two new behavior checks failed before implementation, then passed for all matching links, combined/empty filters, clearing filters, filter-focus preservation, front/behind/outside markers, a rotated canvas, and explicit hide-all. An added close/reopen regression first failed, then passed: closing the bars keeps the criteria applied while showing only the currently focused proxy's links; reopening restores matching-entity links. The scene-reference check confirms the real passthrough canvas, center-eye viewer, marker source, attribute-link flag, and AttributeUI root are assigned. The object/attribute/photo-ratio validator also passed. Scene comparison confirms no changes to existing anchor, input, or layout blocks.

The final Android build succeeded in 53 seconds and was installed on Quest 3 at 2026-10-31 10:21:20 with saved app data preserved. The app was launched as `com.carton22.uistdemo`, PID 7921. APK: `Builds/uistdemo_attribute_links_occlusion.apk`. These checks validate Unity behavior and scene wiring; visual headset confirmation of this update has not yet been reported by the wearer.
