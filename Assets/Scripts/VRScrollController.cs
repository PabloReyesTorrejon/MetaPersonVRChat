using UnityEngine;
using UnityEngine.UI;

public class VRScrollController : MonoBehaviour
{
    public ScrollRect scrollRect;
    public float scrollSpeed = 0.8f;

    void Update()
    {
        if (scrollRect == null) return;

        // Stick izquierdo vertical
        float input = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).y;

        if (Mathf.Abs(input) > 0.1f)
        {
            float newPos = scrollRect.verticalNormalizedPosition + input * scrollSpeed * Time.deltaTime;
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(newPos);
        }
    }
}
