using UnityEngine;

/// <summary>
/// Compatibility wrapper for scenes already serialized against the older left-swipe-specific
/// dismiss component. Prefer <see cref="AttributeUiDismissOnSwipe"/> for new scenes.
/// </summary>
[AddComponentMenu("")]
public class AttributeUiDismissOnLeftSwipe : AttributeUiDismissOnSwipe
{
}
