using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Real Unity-object regression checks, isolated from the open demo scene.</summary>
public static class UistInteractionRegression
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/UIST Demo/Run Interaction Regression Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run checks in Edit mode.");
        var originalScene = SceneManager.GetActiveScene();
        var originalEvents = EventSystem.current;
        var temporaryScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(temporaryScene);
        var results = new List<string>();
        try
        {
            Check("Proxy links survive filter focus and respect visibility", CheckProxyFocus, results);
            Check("OpenXR pinch uses thumb/middle tips, not legacy joint numbers", () => CheckPinch(OVRSkeleton.SkeletonType.XRHandLeft), results);
            Check("Legacy hand skeleton still places at thumb/middle midpoint", () => CheckPinch(OVRSkeleton.SkeletonType.HandLeft), results);
            Check("Midpoint placement waits for both fingertips", CheckMissingThumb, results);
            Check("Successful anchor load hides only the hand placement preview", CheckHandAnchorPreview, results);
            Check("Carousel restores items 1 and 2 after repeated return scrolling", CheckCarouselReturns, results);
            Check("Camera image renders behind the proxy UI and its captions", () => CheckCameraBackdropOrder(originalScene), results);
            Check("Attribute filters link every matching entity and clearing restores single focus", CheckAttributeLinks, results);
            Check("Passthrough canvas occludes marker cubes and number visuals without disabling targets", CheckMarkerOcclusion, results);
            Check("UIST scene wires attribute links and occlusion to the real canvas and viewer", () => CheckSelectionSceneWiring(originalScene), results);
        }
        finally
        {
            foreach (var root in temporaryScene.GetRootGameObjects())
                foreach (var events in root.GetComponentsInChildren<EventSystem>(true))
                    Call(events, "OnDisable");
            if (originalEvents != null) EventSystem.current = originalEvents;
            SceneManager.SetActiveScene(originalScene);
            EditorSceneManager.CloseScene(temporaryScene, true);
        }
        string report = string.Join("\n", results) + "\n";
        File.WriteAllText("Library/UIST-interaction-regression.txt", report);
        if (results.Exists(line => line.StartsWith("FAIL"))) Debug.LogError("[UIST Interaction]\n" + report);
        else Debug.Log("[UIST Interaction]\n" + report);
    }

    private static void Check(string name, Action test, List<string> results)
    {
        try { test(); results.Add("PASS: " + name); }
        catch (Exception e) { results.Add("FAIL: " + name + " — " + (e.InnerException ?? e).Message); }
    }

    private static void CheckProxyFocus()
    {
        var events = new GameObject("Test EventSystem").AddComponent<EventSystem>();
        // EventSystem is not ExecuteAlways; Edit-mode checks must register its lifecycle.
        if (EventSystem.current != events) Call(events, "OnEnable");
        EventSystem.current = events;
        var parent = new GameObject("Test ProxyUI", typeof(RectTransform)).transform;
        var first = MakeLabel(parent, "item1", 0);
        var second = MakeLabel(parent, "item2", 1);
        var filter = new GameObject("Test filter", typeof(RectTransform), typeof(Button));
        var manager = new GameObject("Test manager").AddComponent<ProxyLabelManager>();
        SetField(manager, "m_labelParents", new List<Transform> { parent });
        var flag = new SerializedObject(manager).FindProperty("m_retainProxyFocusWhenUnfocused");
        if (flag != null)
        {
            var settings = new SerializedObject(manager);
            settings.FindProperty("m_retainProxyFocusWhenUnfocused").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        Call(manager, "Awake");
        var visualizer = new GameObject("Test spatial links").AddComponent<CanvasRayIntersectionVisualizer>();
        SetField(visualizer, "m_labelManagers", new List<ProxyLabelManager> { manager });
        events.SetSelectedGameObject(second.gameObject);
        Assert(manager.GetSelectedLabelIndex() == 1, "Expected second proxy selection.");
        Assert(Resolves(visualizer, 1), "Initial spatial link target missing.");
        events.SetSelectedGameObject(filter);
        Assert(Resolves(visualizer, 1), "Spatial link disappeared when focus entered filters.");
        Assert(events.currentSelectedGameObject == filter, "Link retention stole filter input focus.");
        manager.SetVisibleLabelsForMarkerIndices(new[] { 0 }, true);
        Assert(manager.GetSelectedLabelIndex() == 0 && Resolves(visualizer, 0), "Filtered-out target did not fall back to a visible proxy.");
        Assert(!Resolves(visualizer, 1), "A hidden proxy retained a stale spatial link.");
        manager.SetVisibleLabelsForMarkerIndices(Array.Empty<int>(), true);
        Assert(manager.GetSelectedLabelIndex() == -1 && !Resolves(visualizer, 0), "Empty results must have no spatial link.");
        manager.ClearVisibleLabelsFilter();
        Assert(manager.GetSelectedLabelIndex() == 0 && Resolves(visualizer, 0), "Clearing filters did not restore a valid link.");
        Assert(events.currentSelectedGameObject == filter, "Filtering changed the actual UI focus.");
        SetField(manager, "m_retainProxyFocusWhenUnfocused", false);
        Assert(manager.GetSelectedLabelIndex() == -1, "Opt-out must preserve legacy focus behavior.");
    }

    private static RectTransform MakeLabel(Transform parent, string name, int marker)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(LabelMarkerBinding));
        go.transform.SetParent(parent, false);
        go.GetComponent<LabelMarkerBinding>().AddMarkerIndex(marker);
        return go.GetComponent<RectTransform>();
    }

    private static bool Resolves(CanvasRayIntersectionVisualizer visualizer, int marker)
    {
        object[] arguments = { marker, null };
        return (bool)visualizer.GetType().GetMethod("TryGetSelectedLabelRectForMarkerIndex", PrivateInstance).Invoke(visualizer, arguments);
    }

    private static void CheckPinch(OVRSkeleton.SkeletonType type)
    {
        bool xr = type == OVRSkeleton.SkeletonType.XRHandLeft;
        var skeleton = new GameObject("Test skeleton").AddComponent<OVRSkeleton>();
        SetField(skeleton, "_skeletonType", type);
        var thumb = new GameObject("Correct thumb tip").transform;
        var middle = new GameObject("Correct middle tip").transform;
        thumb.position = new Vector3(1, 2, 3);
        middle.position = new Vector3(1.02f, 2.04f, 3.02f);
        var bones = new List<OVRBone>
        {
            new OVRBone(xr ? OVRSkeleton.BoneId.XRHand_ThumbTip : OVRSkeleton.BoneId.Hand_ThumbTip, -1, thumb),
            new OVRBone(xr ? OVRSkeleton.BoneId.XRHand_MiddleTip : OVRSkeleton.BoneId.Hand_MiddleTip, -1, middle)
        };
        if (xr)
        {
            var wrongThumb = new GameObject("Legacy-number ring joint").transform;
            var wrongMiddle = new GameObject("Legacy-number little-finger joint").transform;
            wrongThumb.position = Vector3.zero; wrongMiddle.position = Vector3.zero;
            bones.Add(new OVRBone(OVRSkeleton.BoneId.Hand_ThumbTip, -1, wrongThumb));
            bones.Add(new OVRBone(OVRSkeleton.BoneId.Hand_MiddleTip, -1, wrongMiddle));
        }
        typeof(OVRSkeleton).GetProperty("Bones").SetValue(skeleton, bones);
        var spawner = new GameObject("Test spawner").AddComponent<PinchTargetSpawner>();
        SetField(spawner, "m_leftHandSkeleton", skeleton);
        Call(spawner, "TryResolveTipsFromSkeleton");
        Vector3 actual = (Vector3)Call(spawner, "GetPinchWorldPosition");
        Assert(Vector3.Distance(actual, (thumb.position + middle.position) * .5f) < .000001f,
            "Marker position " + actual + " differs from the tracked fingertip midpoint.");
    }

    private static void CheckMissingThumb()
    {
        var spawner = new GameObject("Missing-thumb spawner").AddComponent<PinchTargetSpawner>();
        SetField(spawner, "m_middleTip", new GameObject("Middle only").transform);
        Assert(float.IsNaN(((Vector3)Call(spawner, "GetPinchWorldPosition")).x), "Missing thumb must defer midpoint placement.");
    }

    private static void CheckHandAnchorPreview()
    {
        var type = typeof(PinchTargetSpawner).Assembly.GetType("HandAnchorPreviewHider");
        Assert(type != null, "No load handler hides the hand-following placement preview.");
        var controller = new GameObject("Right controller").transform;
        var prefab = new GameObject("Anchor placement template");
        var preview = UnityEngine.Object.Instantiate(prefab, controller);
        var otherHandObject = new GameObject("Other hand object");
        otherHandObject.transform.SetParent(controller);
        var restored = UnityEngine.Object.Instantiate(prefab);
        var anchor = restored.AddComponent<OVRSpatialAnchor>();
        var marker = new GameObject("Restored marker");
        marker.transform.SetParent(restored.transform);
        var hider = new GameObject("Preview load handler").AddComponent(type);
        SetField(hider, "m_previewParent", controller);
        SetField(hider, "m_anchorPrefab", prefab);
        var loaded = type.GetMethod("OnAnchorsLoadCompleted");
        loaded.Invoke(hider, new object[] { null });
        loaded.Invoke(hider, new object[] { new List<OVRSpatialAnchor>() });
        loaded.Invoke(hider, new object[] { new List<OVRSpatialAnchor> { null } });
        Assert(preview.activeSelf, "Failed/empty load hid the placement preview.");
        loaded.Invoke(hider, new object[] { new List<OVRSpatialAnchor> { anchor } });
        Assert(!preview.activeSelf, "Hand-following placement preview remains visible after load.");
        Assert(restored.activeSelf && marker.activeInHierarchy, "Restored anchor or markers were disabled.");
        Assert(otherHandObject.activeSelf && prefab.activeSelf, "Other hand objects or anchor template were disabled.");
        loaded.Invoke(hider, new object[] { new List<OVRSpatialAnchor> { anchor } });
        Assert(!preview.activeSelf, "Repeated loads restored the preview.");
    }

    private static void CheckCarouselReturns()
    {
        var events = new GameObject("Wheel EventSystem").AddComponent<EventSystem>();
        if (EventSystem.current != events) Call(events, "OnEnable");
        EventSystem.current = events;
        var root = new GameObject("Test wheel", typeof(RectTransform), typeof(Canvas), typeof(GridLayoutGroup));
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600, 600);
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var grid = root.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(108, 196);
        grid.spacing = new Vector2(16, 24);
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = 1;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Horizon_ProxyLabelWithIcon.prefab");
        var cards = new GameObject[10];
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i] = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rect);
            cards[i].name = "wheel item" + (i + 1);
        }
        var manager = new GameObject("Wheel manager").AddComponent<ProxyLabelManager>();
        SetField(manager, "m_labelParents", new List<Transform> { rect });
        Call(manager, "Awake");
        var wheel = root.AddComponent<ProxyLabelHorizonScroller>();
        wheel.Configure(manager, rect, rect, grid);
        wheel.ApplyProxyUIHorizontalPreset();
        SetField(wheel, "m_visibleRowCount", 3);
        events.SetSelectedGameObject(cards[8]);
        Call(wheel, "OnEnable");
        var animate = typeof(ProxyLabelHorizonScroller).GetMethod("ApplyHorizontalWheelVisuals", PrivateInstance);
        var centerField = typeof(ProxyLabelHorizonScroller).GetField("m_smoothedWindowCenterRow", PrivateInstance);
        float center = (float)centerField.GetValue(wheel);
        float velocity = 0;
        foreach (int selected in new[] { 1, 0, 6, 1, 0, 9, 0, 1 })
        {
            events.SetSelectedGameObject(cards[selected]);
            for (int frame = 0; frame < 90; frame++)
            {
                center = Mathf.SmoothDamp(center, Mathf.Clamp(selected, 1, 8), ref velocity, .12f, Mathf.Infinity, 1f / 72);
                centerField.SetValue(wheel, center);
                animate.Invoke(wheel, new object[] { selected, 10, 1f / 72 });
                Canvas.ForceUpdateCanvases();
            }
            var card = cards[selected];
            var position = ((RectTransform)card.transform).anchoredPosition;
            Assert(!float.IsNaN(position.x) && Mathf.Abs(position.x) < 130, "Returned card moved outside the wheel window.");
            Assert(card.GetComponent<CanvasGroup>().alpha > .99f, "Returned card stayed transparent.");
            foreach (var graphic in card.GetComponentsInChildren<Graphic>(true))
                if (!(graphic is TMPro.TMP_Text))
                    Assert(graphic.enabled && !graphic.canvasRenderer.cull, "Returned graphic stayed disabled or culled: " + graphic.name);
            foreach (var caption in card.GetComponentsInChildren<TMPro.TMP_Text>(true))
                Assert(caption.enabled, "Returned caption stayed disabled.");
        }
        Call(wheel, "OnDisable");
    }

    private static void CheckCameraBackdropOrder(Scene scene)
    {
        Canvas background = null;
        Canvas proxies = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.name == "CameraToWorldCameraCanvas") background = canvas;
                if (canvas.name == "ProxyUI") proxies = canvas;
            }
        Assert(background != null && proxies != null, "Open the UIST scene to check its camera/proxy rendering order.");
        Assert(background.sortingLayerID == proxies.sortingLayerID && background.sortingOrder < proxies.sortingOrder,
            "Camera image and proxy canvas have ambiguous transparent render ordering; camera image can cover cards.");
        foreach (var caption in proxies.GetComponentsInChildren<TMPro.TextMeshPro>(true))
            Assert(caption.GetComponent<MeshRenderer>().sortingOrder > background.sortingOrder,
                "World-space caption can render behind the camera image.");
    }

    private static void CheckAttributeLinks()
    {
        var events = new GameObject("Attribute link events").AddComponent<EventSystem>();
        if (EventSystem.current != events) Call(events, "OnEnable");
        EventSystem.current = events;
        var parent = new GameObject("Attribute link proxies", typeof(RectTransform)).transform;
        for (int i = 0; i < 10; i++) MakeLabel(parent, "item" + (i + 1), i);
        // A grouped label may also contain an ineligible marker: membership still matters.
        parent.GetChild(0).GetComponent<LabelMarkerBinding>().AddMarkerIndex(9);
        var manager = new GameObject("Attribute link manager").AddComponent<ProxyLabelManager>();
        SetField(manager, "m_labelParents", new List<Transform> { parent });
        SetField(manager, "m_retainProxyFocusWhenUnfocused", true);
        Call(manager, "Awake");
        var visualizer = new GameObject("Attribute links").AddComponent<CanvasRayIntersectionVisualizer>();
        SetField(visualizer, "m_labelManagers", new List<ProxyLabelManager> { manager });
        typeof(CanvasRayIntersectionVisualizer).GetField("m_linkAllFilteredMarkers", PrivateInstance)?.SetValue(visualizer, true);
        events.SetSelectedGameObject(parent.GetChild(0).gameObject);
        Assert(manager.GetSelectedLabelIndex() == 0, "Initial proxy focus missing.");
        var filter = new GameObject("Color = Yellow", typeof(RectTransform), typeof(Button));
        typeof(CanvasRayIntersectionVisualizer).GetField("m_attributeUiRoot", PrivateInstance)?.SetValue(visualizer, filter);
        events.SetSelectedGameObject(filter);
        manager.SetVisibleLabelsForMarkerIndices(new[] { 0, 2, 4 }, true);
        foreach (int marker in new[] { 0, 2, 4 })
            Assert(Resolves(visualizer, marker), "Yellow entity item" + (marker + 1) + " has no link.");
        foreach (int marker in new[] { 1, 3, 5, 6, 7, 8, 9 })
            Assert(!Resolves(visualizer, marker), "Nonmatching entity retained an attribute link.");
        filter.SetActive(false);
        events.SetSelectedGameObject(parent.GetChild(2).gameObject);
        Assert(Resolves(visualizer, 2) && !Resolves(visualizer, 0) && !Resolves(visualizer, 4),
            "Closing filter bars must restore links only to the currently focused proxy.");
        Assert(manager.HasVisibleLabelsFilter && parent.GetChild(1).gameObject.activeSelf == false,
            "Closing filter bars cleared the chosen criteria.");
        filter.SetActive(true);
        events.SetSelectedGameObject(filter);
        Assert(Resolves(visualizer, 0) && Resolves(visualizer, 2) && Resolves(visualizer, 4),
            "Reopening filter bars did not restore all matching links.");
        events.SetSelectedGameObject(parent.GetChild(0).gameObject);
        manager.GetSelectedLabelIndex();
        events.SetSelectedGameObject(filter);
        manager.SetVisibleLabelsForMarkerIndices(new[] { 0, 2 }, true); // Yellow AND CS
        Assert(Resolves(visualizer, 0) && Resolves(visualizer, 2) && !Resolves(visualizer, 4), "Combined filters did not limit links to the intersection.");
        manager.SetVisibleLabelsForMarkerIndices(Array.Empty<int>(), true);
        for (int i = 0; i < 10; i++) Assert(!Resolves(visualizer, i), "Empty filter results retained a line.");
        manager.ClearVisibleLabelsFilter();
        Assert(Resolves(visualizer, 0) && !Resolves(visualizer, 2), "Clearing filters did not restore single-proxy links.");
        Assert(events.currentSelectedGameObject == filter, "Attribute links stole filter input focus.");
    }

    private static void CheckMarkerOcclusion()
    {
        var canvas = new GameObject("Occluding passthrough canvas", typeof(RectTransform)).GetComponent<RectTransform>();
        canvas.position = Vector3.forward;
        canvas.sizeDelta = new Vector2(2, 2);
        var viewer = new GameObject("Viewer").transform;
        var parent = new GameObject("Occlusion marker pool").transform;
        var marker = new GameObject("Green cube", typeof(MeshRenderer));
        marker.transform.SetParent(parent);
        var number = new GameObject("Marker number", typeof(MeshRenderer), typeof(Canvas));
        number.transform.SetParent(marker.transform);
        var numberImage = new GameObject("Number UI", typeof(RectTransform), typeof(Image));
        numberImage.transform.SetParent(number.transform);
        var spawner = new GameObject("Occlusion spawner").AddComponent<PinchTargetSpawner>();
        SetField(spawner, "m_spawnParent", parent);
        var hider = new GameObject("Occlusion hider").AddComponent<WorldMarkerVisualHider>();
        SetField(hider, "m_pinchTargetSpawner", spawner);
        var type = typeof(WorldMarkerVisualHider);
        type.GetField("m_hideBehindPassthroughCanvas", PrivateInstance)?.SetValue(hider, true);
        type.GetField("m_occludingCanvas", PrivateInstance)?.SetValue(hider, canvas);
        type.GetField("m_viewer", PrivateInstance)?.SetValue(hider, viewer);
        marker.transform.position = new Vector3(0, 0, 2);
        Call(hider, "ApplyVisibility");
        Assert(!marker.GetComponent<MeshRenderer>().enabled && !number.GetComponent<MeshRenderer>().enabled
            && !number.GetComponent<Canvas>().enabled && !numberImage.GetComponent<Image>().enabled,
            "Cube or number stayed visible behind the passthrough canvas.");
        Assert(marker.activeInHierarchy && spawner.GetMarkerParentTransform().GetChild(0) == marker.transform,
            "Occlusion removed the spatial target.");
        foreach (Vector3 position in new[] { new Vector3(0, 0, .5f), new Vector3(3, 0, 2) })
        {
            marker.transform.position = position;
            Call(hider, "ApplyVisibility");
            Assert(marker.GetComponent<MeshRenderer>().enabled && number.GetComponent<MeshRenderer>().enabled,
                "Marker in front of or outside the canvas did not become visible again.");
        }
        canvas.rotation = Quaternion.Euler(0, 45, 0);
        marker.transform.position = canvas.position * 2;
        Call(hider, "ApplyVisibility");
        Assert(!marker.GetComponent<MeshRenderer>().enabled, "Rotated canvas did not occlude a marker behind it.");
        hider.SetHideWorldMarkers(true);
        marker.transform.position = new Vector3(0, 0, .2f);
        Call(hider, "ApplyVisibility");
        Assert(!marker.GetComponent<MeshRenderer>().enabled, "Explicit hide-all was overridden by occlusion visibility.");
    }

    private static void CheckSelectionSceneWiring(Scene scene)
    {
        WorldMarkerVisualHider hider = null;
        CanvasRayIntersectionVisualizer links = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (hider == null) hider = root.GetComponentInChildren<WorldMarkerVisualHider>(true);
            if (links == null) links = root.GetComponentInChildren<CanvasRayIntersectionVisualizer>(true);
        }
        Assert(hider != null && links != null, "UIST marker/link components are missing.");
        var settings = new SerializedObject(hider);
        Assert(settings.FindProperty("m_hideBehindPassthroughCanvas").boolValue, "Scene occlusion is disabled.");
        var canvas = settings.FindProperty("m_occludingCanvas").objectReferenceValue as RectTransform;
        var viewer = settings.FindProperty("m_viewer").objectReferenceValue as Transform;
        Assert(canvas != null && canvas.name == "CameraToWorldCameraCanvas", "Occluding canvas reference did not resolve.");
        Assert(viewer != null && viewer.name == "CenterEyeAnchor", "Occlusion viewer is not the center-eye anchor.");
        Assert(settings.FindProperty("m_pinchTargetSpawner").objectReferenceValue != null, "Marker pool source is unassigned.");
        var linkSettings = new SerializedObject(links);
        Assert(linkSettings.FindProperty("m_linkAllFilteredMarkers").boolValue, "Scene attribute links are disabled.");
        var attributeUi = linkSettings.FindProperty("m_attributeUiRoot").objectReferenceValue as GameObject;
        Assert(attributeUi != null && attributeUi.name == "AttributeUI", "Attribute selection is not gated by the real filter-bar root.");
    }

    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
    private static object Call(object target, string name) => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
