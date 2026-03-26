using UnityEngine;

/// <summary>
/// Drop-in for ProxyUI (GridLayoutGroup + proxy label children): turns the row into a horizontal
/// scroll wheel with smooth motion and the EventSystem selection kept in the same center slot.
/// Implements behavior via <see cref="ProxyLabelHorizonScroller"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ProxyLabelHorizonScroller))]
public class ProxyUIHorizontalScrollWheel : MonoBehaviour
{
    [SerializeField] private ProxyLabelHorizonScroller m_scroller;
    [SerializeField] private bool m_useMetaRayBanPreset;

    private void Reset()
    {
        EnsureScrollerReference();
        ApplyPreset();
    }

    private void OnEnable()
    {
        EnsureScrollerReference();
        ApplyPreset();
    }

    /// <summary>
    /// Rebuilds layout and snaps the wheel to the current selection immediately (e.g. after toggling labels).
    /// </summary>
    public void ForceRefreshNow()
    {
        EnsureScrollerReference();
        m_scroller.ForceRefreshNow();
    }

    public ProxyLabelHorizonScroller Scroller
    {
        get
        {
            EnsureScrollerReference();
            return m_scroller;
        }
    }

    private void EnsureScrollerReference()
    {
        if (m_scroller == null)
            m_scroller = GetComponent<ProxyLabelHorizonScroller>();
    }

    private void ApplyPreset()
    {
        if (m_scroller == null)
            return;

        if (m_useMetaRayBanPreset)
            m_scroller.ApplyMetaRayBanHorizontalPreset();
        else
            m_scroller.ApplyProxyUIHorizontalPreset();
    }
}
