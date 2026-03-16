using UnityEngine;

/// <summary>
/// Attaches a label index to a spawned marker so systems can filter visibility by label selection.
/// </summary>
public class MarkerLabelBinding : MonoBehaviour
{
    [Tooltip("0-based label index this marker belongs to. -1 means unassigned.")]
    [SerializeField] private int m_labelIndex = -1;

    public int LabelIndex
    {
        get => m_labelIndex;
        set => m_labelIndex = value;
    }
}

