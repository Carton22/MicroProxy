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

    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
    private static object Call(object target, string name) => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
