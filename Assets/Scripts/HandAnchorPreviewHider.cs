using System.Collections.Generic;
using UnityEngine;

/// <summary>Hides the Meta sample's hand-following placement preview after anchors load.</summary>
public class HandAnchorPreviewHider : MonoBehaviour
{
    [Tooltip("The rightControllerAnchor used by SpatialAnchorSpawnerBuildingBlock.")]
    [SerializeField] private Transform m_previewParent;
    [Tooltip("The same anchor template assigned to SpatialAnchorSpawnerBuildingBlock.")]
    [SerializeField] private GameObject m_anchorPrefab;

    public void OnAnchorsLoadCompleted(List<OVRSpatialAnchor> anchors)
    {
        if (anchors == null || !anchors.Exists(anchor => anchor != null)
            || m_previewParent == null || m_anchorPrefab == null)
            return;

        // The SDK creates its preview as a direct controller child. Real anchors are
        // instantiated separately and have OVRSpatialAnchor; never disable those.
        string previewName = m_anchorPrefab.name + "(Clone)";
        for (int i = 0; i < m_previewParent.childCount; i++)
        {
            var child = m_previewParent.GetChild(i);
            if (child.name == previewName && child.GetComponent<OVRSpatialAnchor>() == null)
                child.gameObject.SetActive(false);
        }
    }
}
