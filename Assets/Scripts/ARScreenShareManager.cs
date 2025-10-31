using UnityEngine;
using UnityEngine.UI;
using Agora.Rtc;
using System.Collections;
using TMPro;

public class ARScreenShareManager : MonoBehaviour
{
    [Header("AR Camera")]
    [SerializeField] public Camera arCamera;

    [Header("Video Surfaces")]
    [SerializeField] private RawImage remoteUserView;

    [Header("Capture Settings")]
    [SerializeField] private int captureFrameRate = 20;

    [Header("Debug")]
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private bool showDebug = true;

    private RenderTexture renderTexture;
    private Texture2D frameTexture;
    private byte[] frameBuffer;

    private float captureInterval;
    private float lastCaptureTime;
    private bool isSharing = false;
    private bool isProcessingFrame = false;

    // Agora
    private uint remoteUID = 0;
    private VideoSurface remoteVideoSurfaceComponent;

    private int framesProcessed = 0;
    private int framesFailed = 0;
    private float lastDebugUpdate = 0;
    private long frameTimestamp = 0;

    private int captureWidth;
    private int captureHeight;

    void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main;

        if (arCamera == null)
        {
            Debug.LogError("[ARScreenShare] No AR Camera found!");
            return;
        }

        // Match AR camera aspect ratio
        captureWidth = 960; // base width
        captureHeight = Mathf.RoundToInt(captureWidth / arCamera.aspect);

        captureInterval = 1f / captureFrameRate;
        SetupRenderTexture();
        SetupRemoteViewLayout();

        Debug.Log($"[ARScreenShare] Initialized: {captureWidth}x{captureHeight} @ {captureFrameRate}fps");

        StartCoroutine(InitializeWithDelay());
    }

    private IEnumerator InitializeWithDelay()
    {
        yield return new WaitForSeconds(0.5f);

        if (AgoraManager.Instance == null)
            Debug.LogError("[ARScreenShare] AgoraManager not found!");
    }

    void SetupRenderTexture()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        if (frameTexture != null)
            Destroy(frameTexture);

        renderTexture = new RenderTexture(captureWidth, captureHeight, 24)
        {
            format = RenderTextureFormat.ARGB32,
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        renderTexture.Create();

        frameTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        frameBuffer = new byte[captureWidth * captureHeight * 4];
        Debug.Log($"[ARScreenShare] Buffer allocated: {frameBuffer.Length / 1024}KB");
    }

    void SetupRemoteViewLayout()
    {
        if (remoteUserView == null) return;

        RectTransform rect = remoteUserView.GetComponent<RectTransform>();
        if (rect != null)
        {
            float aspect = captureWidth / (float)captureHeight;
            rect.sizeDelta = new Vector2(320 * aspect, 320); // maintain aspect ratio
        }

        remoteUserView.gameObject.SetActive(true);
        remoteUserView.color = new Color(0.2f, 0.2f, 0.2f, 1f);
    }

    public void StartScreenSharing()
    {
        if (isSharing) return;

        isSharing = true;
        lastCaptureTime = Time.time;
        framesProcessed = 0;
        framesFailed = 0;
        Debug.Log("[ARScreenShare] ✓ Started sharing");
    }

    public void StopScreenSharing()
    {
        isSharing = false;
        isProcessingFrame = false;
        Debug.Log($"[ARScreenShare] Stopped. Sent: {framesProcessed}, Failed: {framesFailed}");
    }

    void Update()
    {
        if (isSharing && !isProcessingFrame && Time.time - lastCaptureTime >= captureInterval)
        {
            lastCaptureTime = Time.time;
            CaptureAndSendFrame();
        }

        if (showDebug && debugText != null && Time.time - lastDebugUpdate > 0.5f)
        {
            lastDebugUpdate = Time.time;
            float successRate = framesProcessed > 0 ? (framesProcessed / (float)(framesProcessed + framesFailed)) * 100f : 0;
            debugText.text = $"AR Screen Share\n" +
                             $"Sharing: {isSharing}\n" +
                             $"Sent: {framesProcessed}\n" +
                             $"Failed: {framesFailed}\n" +
                             $"Success: {successRate:F1}%\n" +
                             $"FPS: {1f / Time.deltaTime:F0}";
        }
    }

    void CaptureAndSendFrame()
    {
        if (isProcessingFrame) return;
        isProcessingFrame = true;

        try
        {
            RenderTexture previousRT = arCamera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            arCamera.targetTexture = renderTexture;
            arCamera.Render();
            arCamera.targetTexture = previousRT;

            RenderTexture.active = renderTexture;
            frameTexture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            frameTexture.Apply(false, false);
            RenderTexture.active = previousActive;

            byte[] rawData = frameTexture.GetRawTextureData();
            if (rawData.Length == frameBuffer.Length)
            {
                System.Array.Copy(rawData, frameBuffer, frameBuffer.Length);
                frameTimestamp = (long)(Time.realtimeSinceStartup * 1000);
                PushVideoFrameToAgora();
                framesProcessed++;
            }
            else framesFailed++;
        }
        catch
        {
            framesFailed++;
        }
        finally { isProcessingFrame = false; }
    }

    private void PushVideoFrameToAgora()
    {
        if (AgoraManager.Instance == null) return;
        IRtcEngine engine = AgoraManager.Instance.GetRtcEngine();
        if (engine == null) return;

        ExternalVideoFrame frame = new ExternalVideoFrame
        {
            type = VIDEO_BUFFER_TYPE.VIDEO_BUFFER_RAW_DATA,
            format = VIDEO_PIXEL_FORMAT.VIDEO_PIXEL_RGBA,
            buffer = frameBuffer,
            stride = captureWidth,
            height = captureHeight,
            timestamp = frameTimestamp
        };
        engine.PushVideoFrame(frame);
    }

    public void OnRemoteUserJoined(uint uid)
    {
        remoteUID = uid;
        if (remoteUserView != null)
        {
            remoteUserView.gameObject.SetActive(true);
            remoteUserView.color = Color.white;
        }
        StartCoroutine(SetupRemoteVideoDelayed(uid));
    }

    private IEnumerator SetupRemoteVideoDelayed(uint uid)
    {
        yield return new WaitForSeconds(0.5f);
        SetupRemoteVideo(uid);
    }

    public void OnRemoteUserLeft()
    {
        remoteUID = 0;
        if (remoteUserView != null)
            remoteUserView.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        if (remoteVideoSurfaceComponent != null)
        {
            remoteVideoSurfaceComponent.SetEnable(false);
            Destroy(remoteVideoSurfaceComponent);
            remoteVideoSurfaceComponent = null;
        }
    }

    private void SetupRemoteVideo(uint uid)
    {
        VideoSurface[] surfaces = remoteUserView.gameObject.GetComponents<VideoSurface>();
        foreach (var s in surfaces) Destroy(s);

        StartCoroutine(AddVideoSurfaceComponent(uid));
    }

    private IEnumerator AddVideoSurfaceComponent(uint uid)
    {
        yield return null;

        remoteVideoSurfaceComponent = remoteUserView.gameObject.AddComponent<VideoSurface>();
        remoteVideoSurfaceComponent.SetForUser(uid, AgoraManager.Instance?.ChannelName ?? "", VIDEO_SOURCE_TYPE.VIDEO_SOURCE_REMOTE);
        remoteVideoSurfaceComponent.SetEnable(true);
    }

    void OnDestroy()
    {
        StopScreenSharing();
        if (renderTexture != null) { renderTexture.Release(); Destroy(renderTexture); }
        if (frameTexture != null) Destroy(frameTexture);
        frameBuffer = null;
    }
}
