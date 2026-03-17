using UnityEngine;

/// <summary>
/// Plays audio when the currently selected proxy label changes.
/// Labels should have LabelAudioBinding that explicitly references an AudioSource.
/// </summary>
public class ProxyLabelAudioPlayer : MonoBehaviour
{
    [Header("Selection")]
    [SerializeField] private ProxyLabelManager m_labelManager;

    [Header("Behavior")]
    [Tooltip("If true, stops and restarts the AudioSource even if it's already playing.")]
    [SerializeField] private bool m_restartIfPlaying = false;

    [Tooltip("If true, plays audio when no label is selected (index = -1) by selecting index 0 if available.")]
    [SerializeField] private bool m_selectFirstWhenNoneSelected = false;

    private int m_lastSelectedIndex = int.MinValue;

    private void Reset()
    {
        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
    }

    private void Update()
    {
        if (m_labelManager == null)
            return;

        int selectedIndex = m_labelManager.GetSelectedLabelIndex();

        if (selectedIndex < 0 && m_selectFirstWhenNoneSelected && m_labelManager.GetLabelCount() > 0)
        {
            m_labelManager.SetSelectedLabelByIndex(0);
            selectedIndex = 0;
        }

        if (selectedIndex == m_lastSelectedIndex)
            return;

        m_lastSelectedIndex = selectedIndex;

        if (selectedIndex < 0)
            return;

        var labelRect = m_labelManager.GetLabelRectTransform(selectedIndex);
        if (labelRect == null)
            return;

        var binding = labelRect.GetComponent<LabelAudioBinding>();
        if (binding == null || binding.AudioSource == null)
            return;

        var audioSource = binding.AudioSource;
        if (m_restartIfPlaying && audioSource.isPlaying)
            audioSource.Stop();

        audioSource.Play();
    }
}

