using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class ScanningIndicator : MonoBehaviour
{
    [Header("Rotation & Pulse Settings")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float minScale = 0.8f;
    [SerializeField] private float maxScale = 1.2f;

    [Header("Floating Movement Settings")]
    [SerializeField] private float moveSpeed = 50f;
    [SerializeField] private float maxOffset = 50f;
    [SerializeField] private float scaleFloatSpeed = 1f;

    [Header("UI Elements")]
    [SerializeField] private RawImage scanningImage;
    [SerializeField] private TMP_Text scanningText;

    private RectTransform rectTransform;
    private Vector2 initialPosition;
    private Vector2 targetPosition;
    private float pulseTime = 0f;
    private float floatScaleTime = 0f;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        initialPosition = rectTransform.anchoredPosition;
        SetRandomTargetPosition();

        if (scanningText != null)
        {
            scanningText.text = "Scanning environment...\nMove your device slowly";
        }
    }

    private void Update()
    {
        AnimatePulse();
        AnimateFloating();
    }

    private void AnimatePulse()
    {
        if (scanningImage != null)
        {
            pulseTime += Time.deltaTime * pulseSpeed;
            float scale = Mathf.Lerp(minScale, maxScale, (Mathf.Sin(pulseTime) + 1f) / 2f);
            scanningImage.transform.localScale = Vector3.one * scale;

            // Pulse alpha
            Color color = scanningImage.color;
            color.a = Mathf.Lerp(0.5f, 1f, (Mathf.Sin(pulseTime) + 1f) / 2f);
            scanningImage.color = color;
        }
    }

    private void AnimateFloating()
    {
        // Move smoothly toward target position
        rectTransform.anchoredPosition = Vector2.MoveTowards(rectTransform.anchoredPosition, targetPosition, moveSpeed * Time.deltaTime);


        floatScaleTime += Time.deltaTime * scaleFloatSpeed;
        float scaleFactor = 1f + Mathf.Sin(floatScaleTime) * 0.05f;
        rectTransform.localScale = Vector3.one * scaleFactor;

        // Set a new random position 
        if (Vector2.Distance(rectTransform.anchoredPosition, targetPosition) < 0.1f)
        {
            SetRandomTargetPosition();
        }
    }

    private void SetRandomTargetPosition()
    {
        float offsetX = Random.Range(-maxOffset, maxOffset);
        float offsetY = Random.Range(-maxOffset, maxOffset);
        targetPosition = initialPosition + new Vector2(offsetX, offsetY);
    }
}
