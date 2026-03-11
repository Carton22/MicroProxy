using UnityEngine;
using TMPro;

public class TextUpdate : MonoBehaviour
{
    [SerializeField]
    private TextMeshPro text;

    [SerializeField]
    private string handName = "";

    void Start()
    {
        if (text != null)
            text.text = "Waiting for gesture...";
    }

    public void SetTextTap()
    {
        if (text != null)
            text.text = FormatText("Tap");
    }

    public void SetTextLeft()
    {
        if (text != null)
            text.text = FormatText("Left");
    }

    public void SetTextRight()
    {
        if (text != null)
            text.text = FormatText("Right");
    }

    public void SetTextUp()
    {
        if (text != null)
            text.text = FormatText("Up");
    }

    public void SetTextDown()
    {
        if (text != null)
            text.text = FormatText("Down");
    }

    public void SetTextTwistRight()
    {
        if (text != null)
            text.text = FormatText("Twist Right");
    }

    public void SetTextTwistLeft()
    {
        if (text != null)
            text.text = FormatText("Twist Left");
    }

    public void SetTextStartPinchTwist()
    {
        if (text != null)
            text.text = FormatText("Start Pinch & Twist");
    }

    public void SetTextPinchTwistProgress(float amount)
    {
        if (text == null)
            return;

        float magnitude = Mathf.Abs(amount);
        string direction = "Center";

        if (amount > 0.01f)
            direction = "Right";
        else if (amount < -0.01f)
            direction = "Left";

        float percent = Mathf.Clamp01(magnitude) * 100f;
        text.text = FormatText($"{direction} Twist {percent:0}%");
    }

    public void SetTextEndPinchTwist()
    {
        if (text != null)
            text.text = FormatText("End Pinch & Twist");
    }

    private string FormatText(string gesture)
    {
        if (string.IsNullOrEmpty(handName))
            return gesture;

        return $"{handName}: {gesture}";
    }
}
