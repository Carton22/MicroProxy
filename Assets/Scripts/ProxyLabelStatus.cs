using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles per-label UI behaviour for proxy labels.
/// - Stores analysis data (name + type) provided by ProxyInject.
/// - On click/submit:
///   - If analysis type is not yet known, appends " processing" to the label text.
///   - If analysis type is known, appends the type to the base label text.
/// Attach this to the root GameObject of the label prefab that also has the Button component.
/// </summary>
public class ProxyLabelStatus : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    [SerializeField] private TextMeshPro m_labelText;

    private string m_baseText;
    private string m_nodeType;
    private bool m_suffixApplied;

    private void Awake()
    {
        if (m_labelText == null)
        {
            m_labelText = GetComponentInChildren<TextMeshPro>();
        }

        if (m_labelText != null)
        {
            m_baseText = m_labelText.text;
        }
    }

    private void OnEnable()
    {
        if (m_labelText != null)
        {
            // Reset cached base text whenever the label is (re)enabled.
            m_baseText = m_labelText.text;
            m_suffixApplied = false;
        }
    }

    /// <summary>
    /// Called by ProxyInject when analysis data for this label becomes available.
    /// </summary>
    public void SetAnalysisData(string displayName, string nodeType)
    {
        m_baseText = displayName;
        m_nodeType = nodeType;
        m_suffixApplied = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        HandlePress();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        HandlePress();
    }

    private void HandlePress()
    {
        if (m_labelText == null)
            return;

        if (string.IsNullOrEmpty(m_nodeType))
        {
            // Analysis not available yet – show "processing" once.
            if (!m_suffixApplied)
            {
                m_labelText.text = $"{m_baseText} processing";
                m_suffixApplied = true;
            }
        }
        else
        {
            // Analysis already available – append the type.
            m_labelText.text = string.IsNullOrEmpty(m_nodeType)
                ? m_baseText
                : $"{m_baseText} {m_nodeType}";
            m_suffixApplied = true;
        }
    }
}

