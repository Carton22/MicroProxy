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
