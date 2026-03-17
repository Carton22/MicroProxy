using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hides the on-screen proxy label UI visuals (Images, Text, etc.) while
/// keeping the GameObjects and Selectables active so microgesture / EventSystem
/// interactions can still select them and trigger audio.
/// </summary>
public class ProxyUILabelVisualHider : MonoBehaviour
{
    [Header("Proxy label parents")]
    [Tooltip("Parents whose children are proxy labels (e.g. ProxyUI, or any label parent used by ProxyLabelManager).")]
    [SerializeField] private List<Transform> m_labelParents = new();

    [Header("Visibility")]
    [Tooltip("When true, all Graphics (Image, Text, etc.) under the label parents are hidden, but Selectables remain active.")]
    [SerializeField] private bool m_hideLabels = true;

    private void LateUpdate()
    {
        if (m_labelParents == null || m_labelParents.Count == 0)
            return;

        bool visible = !m_hideLabels;

        for (int i = 0; i < m_labelParents.Count; i++)
        {
            var parent = m_labelParents[i];
            if (parent == null)
                continue;

            SetLabelVisualsVisible(parent, visible);
        }
    }

    /// <summary>
    /// Allows other scripts to show/hide label visuals at runtime.
    /// </summary>
    public void SetHideLabels(bool hide)
    {
        m_hideLabels = hide;
    }

    private static void SetLabelVisualsVisible(Transform root, bool visible)
    {
        if (root == null)
            return;

        var graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].enabled = visible;
        }
    }
}

