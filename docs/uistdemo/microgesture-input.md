# UIST Demo microgesture input

Apply the input components used by `StudyA_Icon_Micro_Partial_BookShelf.unity` to the UIST `Full_Display_Microgesture.unity` scene. Preserve all object images, card dimensions, attribute memberships, marker placement/persistence, anchor controller bindings, and existing UINavigator navigation settings. Leave the sibling touchpad scene and Study A unchanged.

Both hands use `OVRMicrogestureEventSource` and the existing `MicroGestureUINavigatorInput`: SwipeLeft/SwipeRight navigate, SwipeForward/SwipeBackward route to MoveUp/MoveDown, and ThumbTap submits the selected item or filter. Keep the Study A pinch-and-twist thresholds. Right-hand twist uses `ProxyTwistSingleSelect` with 0.08 per step and the UIST ScreenUI root; its legacy range-selection component remains disabled as in Study A. Left-hand twist drives the existing hierarchy drill-down components. Each navigator explicitly references its own twist source and the UIST attribute and proxy roots.

Disable the existing RemoteGestureUINavigatorInput component to stop phone messages from navigating this scene. Preserve the SocketManager object and the other spatial/controller systems. Omit Study A's debug-text event components because their UI targets belong to the reference scene. Keep keyboard debug navigation on the right bridge only so one debug key press produces one action.

Verification: confirm native gesture wiring is absent before migration; verify source hand references and twist thresholds afterward; compare every unrelated serialized scene block with the saved pre-migration snapshot; verify both protected scenes remain byte-identical; import and validate in Unity. Build and run the updated microgesture scene on the connected Quest when available.

## Verification results

Four scene migration checks passed: native sources, hand references and twist thresholds match Study A; every unrelated pre-existing serialized object/component is unchanged; new component references resolve within the UIST scene; and the touchpad and reference scenes are byte-identical. Unity reloaded the scene successfully and its ten-object/attribute/photo-ratio validation passed.

The baseline APK is preserved at `Builds/uistdemo_touchpad_v0.apk`. Physical gesture recognition requires testing in the Quest headset; serialized wiring checks do not establish recognition accuracy.

## Quest build

Android build succeeded in 68 seconds and launched as `com.carton22.uistdemo` on the connected Quest 3. APK: `Builds/uistdemo_microgesture.apk`. The app was confirmed resumed. No microgesture input exception appeared in the captured startup log.

Device verification also exposed a NullReferenceException in the unchanged `CameraToWorldCameraCanvas.Start` coroutine. Its prefab has an unassigned `m_debugText`, which Start dereferences immediately. This camera sample issue was left unchanged to keep this migration limited to input; physical gesture recognition still needs headset testing.
