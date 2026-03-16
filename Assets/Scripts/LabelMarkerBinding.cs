using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attaches a marker index to a label so systems can filter visibility by label selection.
/// </summary>
public class LabelMarkerBinding : MonoBehaviour
{
    [Tooltip("0-based marker index this label belongs to. -1 means unassigned.")]
    [SerializeField] private List<int> m_markerIndices = new();

    public IReadOnlyList<int> MarkerIndices => m_markerIndices;
    public void AddMarkerIndex(int index)
    {
        if (index < 0 || m_markerIndices.Contains(index))
            return;
        m_markerIndices.Add(index);
    }
    public void RemoveMarkerIndex(int index)
    {
        m_markerIndices.Remove(index);
    }
    public void ClearMarkerIndices()
    {
        m_markerIndices.Clear();
    }

}

