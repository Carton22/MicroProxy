using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Scene-scoped authoring and verification for the approved UIST demo catalog.</summary>
public static class UistDemoSceneSetup
{
    private const string ScenePath = "Assets/Scene/UIST Demo/Full_Display_Microgesture.unity";
    private const string CatalogPath = "Assets/Resources/UISTDemo/catalog.json";
    private const string ImageFolder = "Assets/Resources/UISTDemo/Items/";
    private const string ReportPath = "Library/UISTDemo-validation.txt";

    [Serializable] private class Catalog { public Item[] items; }
    [Serializable] private class Item
    {
        public string name;
        public string type;
        public string color;
        public string topic;
        public int price;
    }

    [MenuItem("Tools/UIST Demo/Apply Approved Catalog %#y")]
    public static void Apply()
    {
        RequireTargetScene();
        var items = ReadCatalog();
        var proxy = Find("ProxyUI");
        var attributeUi = Find("AttributeUI");
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply UIST demo catalog");
        Undo.RegisterFullObjectHierarchyUndo(proxy.gameObject, "Update UIST proxy labels");
        Undo.RegisterFullObjectHierarchyUndo(attributeUi.gameObject, "Update UIST attributes");

        var cards = ResizeButtons(proxy, items.Length);
        var grid = proxy.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(108, 196);
        grid.spacing = new Vector2(16, 24);
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = 1;
        Dirty(grid);

        for (int i = 0; i < items.Length; i++)
        {
            var card = cards[i];
            var item = items[i];
            card.name = item.name;
            card.gameObject.SetActive(true);
            card.localScale = Vector3.one;
            SetText(card, item.name);
            SetMarkers(card, new[] { i });

            string imagePath = ImageFolder + item.name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            if (importer == null) throw new InvalidOperationException("Missing image: " + imagePath);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);

            var image = card.GetComponentInChildren<RawImage>(true);
            if (image == null) throw new InvalidOperationException("Missing RawImage: " + item.name);
            image.texture = texture;
            image.uvRect = new Rect(0, 0, 1, 1);
            image.color = Color.white;
            image.raycastTarget = false;
            image.rectTransform.localScale = Vector3.one;
            image.rectTransform.localRotation = Quaternion.identity;
            var frame = (RectTransform)image.transform.parent;
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.pivot = Vector2.one * 0.5f;
            frame.offsetMin = new Vector2(8, 40);
            frame.offsetMax = new Vector2(-8, -8);
            frame.localScale = Vector3.one;
            var fitter = image.GetComponent<AspectRatioFitter>();
            if (fitter == null) fitter = Undo.AddComponent<AspectRatioFitter>(image.gameObject);
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)texture.width / texture.height;
            fitter.enabled = true;
            Dirty(image); Dirty(image.rectTransform); Dirty(frame); Dirty(fitter); Dirty(card);

            foreach (var caption in card.GetComponentsInChildren<TMP_Text>(true))
            {
                var rect = caption.rectTransform;
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(1, 0);
                rect.pivot = Vector2.one * 0.5f;
                rect.anchoredPosition = new Vector2(0, 20);
                rect.sizeDelta = new Vector2(-16, 32);
                rect.localScale = Vector3.one;
                // The prefab uses world-space TextMeshPro, whose glyph units are
                // much smaller than the same numeric size in TextMeshProUGUI.
                caption.fontSize = 200;
                caption.enableAutoSizing = true;
                caption.fontSizeMin = 120;
                caption.fontSizeMax = 220;
                caption.alignment = TextAlignmentOptions.Center;
                caption.overflowMode = TextOverflowModes.Truncate;
                Dirty(rect); Dirty(caption);
            }

            var price = card.GetComponent<ProxyLabelPriceRandomizeOnClick>();
            if (price != null)
            {
                var serialized = new SerializedObject(price);
                serialized.FindProperty("m_useManualFixedValue").boolValue = true;
                serialized.FindProperty("m_manualFixedDollars").intValue = item.price;
                serialized.ApplyModifiedProperties();
                Dirty(price);
            }
        }

        ConfigureOptions("Attribute-Type", new[] { "Book", "Pen", "Box" }, items, item => item.type);
        ConfigureOptions("Attribute-Color", new[] { "Yellow", "Black", "Green", "Red" }, items, item => item.color);
        ConfigureOptions("Attribute-Topic", new[] { "Art", "CS" }, items, item => item.topic);
        PositionCompactBars();
        var names = DirectButtons(Find("AttributeName"));
        string[] order = { "Type", "Color", "Topic" };
        for (int i = 0; i < order.Length; i++)
        {
            var button = names.Single(t => t.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text == order[i]));
            button.SetSiblingIndex(i);
            button.name = order[i];
            SetText(button, order[i]);
            SetMarkers(button, Enumerable.Range(0, items.Length));
            Dirty(button);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)proxy);
        Canvas.ForceUpdateCanvases();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Undo.CollapseUndoOperations(undoGroup);
        Validate();
        if (!File.ReadAllText(ReportPath).StartsWith("PASS:", StringComparison.Ordinal))
            throw new InvalidOperationException("UIST Demo validation failed; the scene was not saved.");
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
            throw new InvalidOperationException("Could not save the UIST Demo scene.");
        Selection.activeGameObject = proxy.gameObject;
        SceneView.RepaintAll();
        Debug.Log("[UIST Demo] Saved ten objects, approved filter bindings, and aspect-preserving photos to " + ScenePath);
    }

    [MenuItem("Tools/UIST Demo/Validate Active Scene %#u")]
    public static void Validate()
    {
        var errors = new List<string>();
        RequireTargetScene();
        var items = ReadCatalog();
        var cards = DirectButtons(Find("ProxyUI"));
        Check(cards.Length == 10, "Expected exactly ten proxy labels; got " + cards.Length, errors);
        for (int i = 0; i < Math.Min(cards.Length, items.Length); i++)
        {
            var card = cards[i];
            var item = items[i];
            Check(card.name == item.name, "Object name/order mismatch for " + item.name, errors);
            Check(card.GetComponentsInChildren<TMP_Text>(true).All(text => text.text == item.name), "Caption mismatch: " + item.name, errors);
            Check(card.GetComponentsInChildren<TMP_Text>(true).All(text => text.fontSizeMax >= 120), "World-space caption is too small: " + item.name, errors);
            CheckMarkers(card, new[] { i }, errors);
            var image = card.GetComponentInChildren<RawImage>(true);
            string expectedPath = ImageFolder + item.name + ".png";
            Check(image != null && AssetDatabase.GetAssetPath(image.texture) == expectedPath, "Texture mismatch: " + item.name, errors);
            if (image == null || image.texture == null) continue;
            var fitter = image.GetComponent<AspectRatioFitter>();
            float ratio = (float)image.texture.width / image.texture.height;
            Check(fitter != null && fitter.enabled && fitter.aspectMode == AspectRatioFitter.AspectMode.FitInParent && Mathf.Abs(fitter.aspectRatio - ratio) < 0.0001f,
                "Photo must fit without stretching: " + item.name, errors);
            Check(image.uvRect == new Rect(0, 0, 1, 1), "Photo must show the full image: " + item.name, errors);
            var rendered = image.rectTransform.rect.size;
            Check(rendered.x > 0 && rendered.y > 0 && Mathf.Abs(rendered.x / rendered.y - ratio) < 0.001f,
                "Rendered photo ratio mismatch: " + item.name, errors);
            Check(image.rectTransform.localScale == Vector3.one && card.localScale == Vector3.one,
                "Non-uniform authored scale: " + item.name, errors);
        }
        ValidateOptions("Attribute-Type", new[] { "Book", "Pen", "Box" }, items, item => item.type, errors);
        ValidateOptions("Attribute-Color", new[] { "Yellow", "Black", "Green", "Red" }, items, item => item.color, errors);
        ValidateOptions("Attribute-Topic", new[] { "Art", "CS" }, items, item => item.topic, errors);
        foreach (var button in DirectButtons(Find("AttributeName")))
            CheckMarkers(button, Enumerable.Range(0, 10), errors);
        string report = errors.Count == 0
            ? "PASS: 10 objects, 10 original photos, 0-based markers 0-9, 9 attribute values with exact memberships, full-image UVs, and FitInParent photo ratios."
            : "FAIL:\n" + string.Join("\n", errors);
        File.WriteAllText(ReportPath, report + "\n");
        if (errors.Count > 0) { Debug.LogError("[UIST Demo] " + report); return; }
        Debug.Log("[UIST Demo] " + report);
    }

    private static void ConfigureOptions(string rootName, string[] values, Item[] items, Func<Item, string> valueOf)
    {
        var buttons = ResizeButtons(Find(rootName), values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            string value = values[i];
            buttons[i].name = value;
            buttons[i].gameObject.SetActive(true);
            SetText(buttons[i], value);
            SetMarkers(buttons[i], Enumerable.Range(0, items.Length).Where(index => valueOf(items[index]) == value));
            Dirty(buttons[i]);
        }
    }

    private static void PositionCompactBars()
    {
        var filters = (RectTransform)Find("AttributeName");
        var total = (RectTransform)Find("Total Price");
        Undo.RecordObjects(new UnityEngine.Object[] { filters, total }, "Separate UIST bars from cards");
        filters.anchoredPosition = new Vector2(0.324f, -0.455f);
        total.anchoredPosition = new Vector2(0.46f, -0.115f);
        StyleBarLabels(filters, new Vector2(140, 48));
        StyleBarLabels(total, new Vector2(180, 48));
        Dirty(filters); Dirty(total);
    }

    [MenuItem("Tools/UIST Demo/Apply Compact Layout")]
    public static void ApplyCompactLayout()
    {
        RequireTargetScene();
        var proxy = (RectTransform)Find("ProxyUI");
        var grid = proxy.GetComponent<GridLayoutGroup>();
        Undo.RecordObject(grid, "Compact UIST cards");
        grid.cellSize = new Vector2(108, 196);
        Dirty(grid);
        PositionCompactBars();
        LayoutRebuilder.ForceRebuildLayoutImmediate(proxy);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[UIST Layout] Saved compact cards, separated bars, and fitted bar text.");
    }

    private static void StyleBarLabels(RectTransform bar, Vector2 cellSize)
    {
        Undo.RegisterFullObjectHierarchyUndo(bar.gameObject, "Fit UIST bar labels");
        var grid = bar.GetComponent<GridLayoutGroup>();
        grid.cellSize = cellSize;
        grid.spacing = new Vector2(16, 8);
        Dirty(grid);
        foreach (var button in DirectButtons(bar))
        {
            button.localScale = Vector3.one;
            Dirty(button);
            foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                var rect = text.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = Vector2.one * 0.5f;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(-16, -8);
                rect.localScale = Vector3.one;
                text.enableAutoSizing = true;
                text.fontSize = text is TextMeshPro ? 200 : 22;
                text.fontSizeMin = text is TextMeshPro ? 120 : 14;
                text.fontSizeMax = text is TextMeshPro ? 200 : 22;
                text.alignment = TextAlignmentOptions.Center;
                text.overflowMode = TextOverflowModes.Truncate;
                Dirty(rect); Dirty(text);
            }
            foreach (var component in button.GetComponents<MonoBehaviour>())
            {
                if (component == null) continue;
                var serialized = new SerializedObject(component);
                var radius = serialized.FindProperty("borderRadius");
                if (radius == null || radius.propertyType != SerializedPropertyType.Vector4) continue;
                radius.vector4Value = Vector4.one * 24;
                serialized.ApplyModifiedProperties();
                Dirty(component);
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(bar);
    }

    private static void ValidateOptions(string name, string[] values, Item[] items, Func<Item, string> valueOf, List<string> errors)
    {
        var buttons = DirectButtons(Find(name));
        Check(buttons.Length == values.Length, name + " option count mismatch", errors);
        for (int i = 0; i < Math.Min(buttons.Length, values.Length); i++)
        {
            string value = values[i];
            Check(buttons[i].name == value && buttons[i].GetComponentsInChildren<TMP_Text>(true).All(text => text.text == value), name + " option mismatch: " + value, errors);
            CheckMarkers(buttons[i], Enumerable.Range(0, items.Length).Where(index => valueOf(items[index]) == value), errors);
        }
    }

    private static Transform[] ResizeButtons(Transform parent, int count)
    {
        var buttons = DirectButtons(parent).ToList();
        if (buttons.Count == 0) throw new InvalidOperationException("No label template under " + parent.name);
        while (buttons.Count < count)
        {
            var clone = UnityEngine.Object.Instantiate(buttons[0].gameObject, parent);
            Undo.RegisterCreatedObjectUndo(clone, "Add UIST attribute option");
            buttons.Add(clone.transform);
        }
        for (int i = buttons.Count - 1; i >= count; i--)
            Undo.DestroyObjectImmediate(buttons[i].gameObject);
        return DirectButtons(parent);
    }

    private static void SetText(Transform root, string value)
    {
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) { text.text = value; Dirty(text); }
    }

    private static void SetMarkers(Transform root, IEnumerable<int> indices)
    {
        var binding = root.GetComponent<LabelMarkerBinding>();
        if (binding == null) binding = Undo.AddComponent<LabelMarkerBinding>(root.gameObject);
        binding.ClearMarkerIndices();
        foreach (int index in indices) binding.AddMarkerIndex(index);
        Dirty(binding);
    }

    private static void CheckMarkers(Transform root, IEnumerable<int> expected, List<string> errors)
    {
        var binding = root.GetComponent<LabelMarkerBinding>();
        Check(binding != null && binding.MarkerIndices.SequenceEqual(expected), "Marker membership mismatch: " + root.name, errors);
    }

    private static void Check(bool condition, string message, List<string> errors) { if (!condition) errors.Add(message); }
    private static Transform[] DirectButtons(Transform root) => root.Cast<Transform>().Where(child => child.GetComponent<Button>() != null).ToArray();
    private static Transform Find(string name) => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Single(transform => transform.name == name);
    private static Item[] ReadCatalog()
    {
        var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(CatalogPath));
        if (catalog?.items == null || catalog.items.Length != 10) throw new InvalidOperationException("Expected the approved ten-item UIST catalog.");
        return catalog.items;
    }
    private static void RequireTargetScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Open the UIST Demo Full_Display_Microgesture scene in Edit mode first.");
    }
    private static void Dirty(UnityEngine.Object value)
    {
        EditorUtility.SetDirty(value);
        if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
    }
}
