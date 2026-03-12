using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Simple shared logger that writes to the Unity console and,
/// optionally, appends lines into a TextMeshPro UI element.
/// Attach this once in a scene and assign it from other scripts.
/// </summary>
public class SharedLogger : MonoBehaviour
{
    [Header("Output Target")]
    [SerializeField] private TextMeshPro m_textTarget;
    [SerializeField] private bool m_showDebugLog = false;

    public void Log(string message) => LogInternal(message, false);

    public void LogError(string message) => LogInternal(message, true);

    public void LogInternal(string message, bool isError)
    {
        if (isError)
        {
            Debug.LogError(message);
        }
        else
        {
            Debug.Log(message);
        }

        if (!m_showDebugLog || m_textTarget == null)
        {
            return;
        }

        var line = $"{message}";
        m_textTarget.text = string.IsNullOrEmpty(m_textTarget.text)
            ? line
            : $"{m_textTarget.text}\n{line}";
    }
}

