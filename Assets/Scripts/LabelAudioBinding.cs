using UnityEngine;

/// <summary>
/// Put this on a proxy label GameObject to bind it to exactly one AudioSource.
/// Used by ProxyLabelAudioPlayer to play audio when the label is selected.
/// </summary>
public class LabelAudioBinding : MonoBehaviour
{
    [SerializeField] private AudioSource m_audioSource;

    public AudioSource AudioSource => m_audioSource;

    private void Reset()
    {
        if (m_audioSource == null)
            m_audioSource = GetComponentInChildren<AudioSource>(true);
    }
}

