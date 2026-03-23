using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// For UI proxy labels that use an Image with a Material.
/// Creates a per-instance material at runtime and updates a color property when selected/deselected.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class ProxyLabelMaterialColorOnSelect : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [Header("Target")]
    [SerializeField] private Image m_image;

    [Header("Material color")]
    [Tooltip("Shader color property to set. Common values: _Color, _BaseColor.")]
    [SerializeField] private string m_colorProperty = "_Color";

    [SerializeField] private Color m_normalColor = new Color(0.75f, 0.85f, 0.9f, 1f);
    [SerializeField] private Color m_selectedColor = new Color(1f, 0.8f, 0.2f, 1f);
    [Tooltip("Used when older prefab data still has the same color for both normal and selected states.")]
    [SerializeField] private Color m_fallbackSelectedColor = new Color(1f, 0.8f, 0.2f, 1f);

    private Material m_instanceMaterial;

    private void Awake()
    {
        if (m_image == null)
            m_image = GetComponent<Image>();

        if (m_image == null || m_image.material == null)
            return;

        // Clone material so changing color only affects this label instance.
        m_instanceMaterial = Instantiate(m_image.material);
        m_image.material = m_instanceMaterial;

        ApplyColor(GetNormalColor());
    }

    private void OnEnable()
    {
        // In case selection already exists when enabling.
        bool selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        ApplyColor(selected ? GetSelectedColor() : GetNormalColor());
    }

    public void OnSelect(BaseEventData eventData)
    {
        ApplyColor(GetSelectedColor());
    }

    public void OnDeselect(BaseEventData eventData)
    {
        ApplyColor(GetNormalColor());
    }

    private void ApplyColor(Color c)
    {
        if (m_instanceMaterial == null)
        {
            if (m_image != null)
                m_image.color = c;
            return;
        }

        if (!string.IsNullOrEmpty(m_colorProperty) && m_instanceMaterial.HasProperty(m_colorProperty))
        {
            m_instanceMaterial.SetColor(m_colorProperty, c);
        }
        else
        {
            // Fallback: many UI shaders use _Color.
            if (m_instanceMaterial.HasProperty("_Color"))
                m_instanceMaterial.SetColor("_Color", c);
        }
    }

    private Color GetNormalColor() => m_normalColor;

    private Color GetSelectedColor()
    {
        if (ColorsApproximatelyEqual(m_selectedColor, m_normalColor))
            return m_fallbackSelectedColor;

        return m_selectedColor;
    }

    private static bool ColorsApproximatelyEqual(Color a, Color b)
    {
        const float epsilon = 0.01f;
        return Mathf.Abs(a.r - b.r) <= epsilon
            && Mathf.Abs(a.g - b.g) <= epsilon
            && Mathf.Abs(a.b - b.b) <= epsilon
            && Mathf.Abs(a.a - b.a) <= epsilon;
    }
}
