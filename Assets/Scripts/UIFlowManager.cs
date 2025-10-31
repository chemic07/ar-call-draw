using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIFlowManager : MonoBehaviour
{
    public static UIFlowManager Instance { get; private set; }

    [Header("Screens")]
    [SerializeField] private GameObject splashScreen;
    [SerializeField] private GameObject inputScreen;
    [SerializeField] private GameObject arScreen;

    [Header("Input Screen Elements")]
    [SerializeField] private TMP_InputField userIdInput;
    [SerializeField] private TMP_InputField channelNameInput;
    [SerializeField] private TMP_InputField rtcTokenInput;
    [SerializeField] private TMP_InputField rtmTokenInput;
    [SerializeField] private Button startButton;

    [Header("AR Screen Elements")]
    [SerializeField] private GameObject scanningIndicator;
    [SerializeField] private GameObject colorPanel;
    [SerializeField] private GameObject controlPanel;
    [SerializeField] private Button colorPanelToggleButton;
    [SerializeField] private Button joinCallButton;
    [SerializeField] private Button leaveCallButton;

    [Header("Color Panel Elements")]
    [SerializeField] private Button redButton;
    [SerializeField] private Button blueButton;
    [SerializeField] private Button greenButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Slider widthSlider;
    [SerializeField] private Slider distanceSlider;
    [SerializeField] private Button closePanelButton;
    [SerializeField] private Image currentColorIndicator;
    [SerializeField] private TMP_Text widthValueText;
    [SerializeField] private TMP_Text distanceValueText;

    [Header("Control Panel Elements")]
    [SerializeField] private Button muteAudioButton;
    [SerializeField] private Button muteVideoButton;
    [SerializeField] private TMP_Text connectionStatusText;
    [SerializeField] private Button debugToggleButton;
    [SerializeField] private TMP_Text debugButtonText;

    [Header("Settings")]
    [SerializeField] private float splashDuration = 2f;

    private bool isTracking = false;
    private bool isCallActive = false;
    private bool isColorPanelOpen = false;
    private bool isAudioMuted = false;
    private bool isVideoMuted = false;
    private bool isDebugPanelVisible = false;
    private Color currentDrawingColor = Color.red;


    private string storedUserId;
    private string storedChannelName;
    private string storedRtcToken;
    private string storedRtmToken;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        InitializeUI();
        StartCoroutine(ShowSplashScreen());
    }

    private void InitializeUI()
    {
        if (splashScreen) splashScreen.SetActive(false);
        if (inputScreen) inputScreen.SetActive(false);
        if (arScreen) arScreen.SetActive(false);

        if (scanningIndicator) scanningIndicator.SetActive(false);
        if (colorPanel) colorPanel.SetActive(false);
        if (controlPanel) controlPanel.SetActive(false);
        if (colorPanelToggleButton) colorPanelToggleButton.gameObject.SetActive(true);

        SetupButtonListeners();

        if (currentColorIndicator) currentColorIndicator.color = currentDrawingColor;
        if (widthSlider) widthSlider.value = 0.01f;
        if (distanceSlider) distanceSlider.value = 1.5f;

        UpdateWidthText(0.01f);
        UpdateDistanceText(1.5f);
        UpdateDebugButtonText();
    }

    private void SetupButtonListeners()
    {
        if (startButton) startButton.onClick.AddListener(OnStartButtonClicked);
        if (joinCallButton) joinCallButton.onClick.AddListener(OnJoinCallClicked);
        if (leaveCallButton) leaveCallButton.onClick.AddListener(OnLeaveCallClicked);
        if (colorPanelToggleButton) colorPanelToggleButton.onClick.AddListener(ToggleColorPanel);
        if (redButton) redButton.onClick.AddListener(() => SelectColor(Color.red));
        if (blueButton) blueButton.onClick.AddListener(() => SelectColor(Color.blue));
        if (greenButton) greenButton.onClick.AddListener(() => SelectColor(Color.green));
        if (clearButton) clearButton.onClick.AddListener(ClearAllDrawings);
        if (closePanelButton) closePanelButton.onClick.AddListener(CloseColorPanel);
        if (widthSlider) widthSlider.onValueChanged.AddListener(OnWidthChanged);
        if (distanceSlider) distanceSlider.onValueChanged.AddListener(OnDistanceChanged);
        if (muteAudioButton) muteAudioButton.onClick.AddListener(ToggleMuteAudio);
        if (muteVideoButton) muteVideoButton.onClick.AddListener(ToggleMuteVideo);
        if (debugToggleButton) debugToggleButton.onClick.AddListener(ToggleDebugPanel);
    }


    #region splash and Input Screen
    private IEnumerator ShowSplashScreen()
    {
        if (splashScreen) splashScreen.SetActive(true);
        yield return new WaitForSeconds(splashDuration);

        if (splashScreen) splashScreen.SetActive(false);
        ShowInputScreen();
    }

    private void ShowInputScreen()
    {
        if (inputScreen) inputScreen.SetActive(true);
        Debug.Log("[UIFlow] Showing input screen");
    }

    private void OnStartButtonClicked()
    {
        string userId = userIdInput?.text ?? "";
        string channelName = channelNameInput?.text ?? "";
        string rtcToken = rtcTokenInput?.text ?? "";  //RTC Token
        string rtmToken = rtmTokenInput?.text ?? "";  // RTM Token

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(channelName))
        {
            Debug.LogWarning("[UIFlow] User ID and Channel Name are required!");
            UpdateConnectionStatus("Please fill required fields");
            return;
        }

        storedUserId = userId;
        storedChannelName = channelName;
        storedRtcToken = rtcToken;
        storedRtmToken = rtmToken;

        PlayerPrefs.SetString("UserId", userId);
        PlayerPrefs.SetString("ChannelName", channelName);
        PlayerPrefs.SetString("RtcToken", rtcToken);
        PlayerPrefs.SetString("RtmToken", rtmToken);
        PlayerPrefs.Save();

        Debug.Log($"[UIFlow] Credentials stored - User: {userId}, Channel: {channelName}");
        Debug.Log($"[UIFlow] RTC Token: {(string.IsNullOrEmpty(rtcToken) ? "None" : "Present")}");
        Debug.Log($"[UIFlow] RTM Token: {(string.IsNullOrEmpty(rtmToken) ? "None" : "Present")}");

        // init afora 
        InitializeAgoraAndRTM();

        if (inputScreen) inputScreen.SetActive(false);
        ShowARScreen();
    }

    #endregion

    #region init RTM and RTC

    private void InitializeAgoraAndRTM()
    {
        // rtm and rtc
        if (AgoraManager.Instance != null)
        {
            AgoraManager.Instance.SetCredentials(storedUserId, storedChannelName, storedRtcToken);
            Debug.Log($"[UIFlow] Agora RTC credentials set with RTC token");

            PerformanceDiagnostics.Instance?.LogSystem($"Agora RTC initialized - User: {storedUserId}, Channel: {storedChannelName}");
        }
        else
        {
            Debug.LogError("[UIFlow] AgoraManager not found in scene!");
            PerformanceDiagnostics.Instance?.LogSystem("AgoraManager not found!");
        }


        var rtmManager = FindFirstObjectByType<AgoraRTMSimpleTest>();
        if (rtmManager != null)
        {
            rtmManager.SetCredentials(storedUserId, storedChannelName, storedRtmToken);
            Debug.Log($"[UIFlow] RTM credentials set with RTM token");

            PerformanceDiagnostics.Instance?.LogSystem($"RTM credentials set - User: {storedUserId}, Channel: {storedChannelName}");
        }
        else
        {
            Debug.LogError("[UIFlow] RTM Manager not found in scene!");
            PerformanceDiagnostics.Instance?.LogSystem("RTM Manager not found!");
        }
    }

    #endregion

    private void ShowARScreen()
    {
        if (arScreen) arScreen.SetActive(true);

        if (scanningIndicator) scanningIndicator.SetActive(true);
        if (controlPanel) controlPanel.SetActive(false);
        if (colorPanelToggleButton) colorPanelToggleButton.gameObject.SetActive(true);
        if (colorPanel) colorPanel.SetActive(false);
        isColorPanelOpen = false;

        Debug.Log("[UIFlow] AR Screen active - Start tracking planes");
        UpdateConnectionStatus("Scan your environment...");

        PerformanceDiagnostics.Instance?.LogSystem("AR Screen active - Scanning for planes");
    }


    public void OnTrackingStarted()
    {
        isTracking = true;

        if (scanningIndicator) scanningIndicator.SetActive(false);

        Debug.Log("[UIFlow] Tracking started - Scanning indicator hidden");
        UpdateConnectionStatus("Tracking active. Ready to join call.");

        PerformanceDiagnostics.Instance?.LogSystem("✓ Plane tracking established");
    }

    public void OnTrackingLost()
    {
        isTracking = false;

        if (scanningIndicator && !isCallActive)
            scanningIndicator.SetActive(true);

        Debug.Log("[UIFlow] Tracking lost");

        PerformanceDiagnostics.Instance?.LogSystem("⚠ Tracking lost");
    }

    #region Call Controls
    private void OnJoinCallClicked()
    {
        if (AgoraManager.Instance != null)
        {
            AgoraManager.Instance.JoinChannel(storedChannelName);

            PerformanceDiagnostics.Instance?.LogRTCEvent("Join Channel Requested", storedChannelName);
        }

        isCallActive = true;

        if (controlPanel) controlPanel.SetActive(true);
        if (joinCallButton) joinCallButton.gameObject.SetActive(false);
        if (leaveCallButton) leaveCallButton.gameObject.SetActive(true);

        UpdateConnectionStatus("Connecting to call...");
        Debug.Log("[UIFlow] Joining call...");
    }

    private void OnLeaveCallClicked()
    {
        if (AgoraManager.Instance != null)
        {
            AgoraManager.Instance.LeaveChannel();

            PerformanceDiagnostics.Instance?.LogRTCEvent("Leave Channel Requested");
        }

        isCallActive = false;

        if (controlPanel) controlPanel.SetActive(false);
        if (joinCallButton) joinCallButton.gameObject.SetActive(true);
        if (leaveCallButton) leaveCallButton.gameObject.SetActive(false);

        UpdateConnectionStatus("Call ended");
        Debug.Log("[UIFlow] Left call");

        PerformanceDiagnostics.Instance?.LogSystem("Call ended by user");
    }

    public void OnCallJoined(string channel, uint uid)
    {
        UpdateConnectionStatus($"Connected: {channel} (UID: {uid})");

        PerformanceDiagnostics.Instance?.LogRTCEvent("Call Joined Successfully", $"UID: {uid}");
    }

    public void OnRemoteUserJoined()
    {
        UpdateConnectionStatus("Remote user joined!");

        PerformanceDiagnostics.Instance?.LogRTCEvent("Remote User Joined");
    }

    public void OnRemoteUserLeft()
    {
        UpdateConnectionStatus("Remote user left");

        PerformanceDiagnostics.Instance?.LogRTCEvent("Remote User Left");
    }


    #endregion

    #region Color Panel
    private void ToggleColorPanel()
    {
        isColorPanelOpen = !isColorPanelOpen;

        if (colorPanel)
            colorPanel.SetActive(isColorPanelOpen);

        if (colorPanelToggleButton)
            colorPanelToggleButton.gameObject.SetActive(!isColorPanelOpen);

        Debug.Log($"[UIFlow] Color panel {(isColorPanelOpen ? "opened" : "closed")}");
    }

    private void CloseColorPanel()
    {
        isColorPanelOpen = false;

        if (colorPanel)
            colorPanel.SetActive(false);

        if (colorPanelToggleButton)
            colorPanelToggleButton.gameObject.SetActive(true);

        Debug.Log("[UIFlow] Color panel closed");
    }

    private void SelectColor(Color color)
    {
        currentDrawingColor = color;

        if (currentColorIndicator)
            currentColorIndicator.color = color;

        var drawingManager = FindFirstObjectByType<ARDrawingManager>();
        if (drawingManager != null)
        {
            drawingManager.SetDrawingColor(color);
        }

        string colorName = color == Color.red ? "Red" :
                          color == Color.blue ? "Blue" :
                          color == Color.green ? "Green" : "Unknown";

        Debug.Log($"[UIFlow] Color selected: {colorName}");

        PerformanceDiagnostics.Instance?.LogDrawingLocal($"Color changed to {colorName}");
    }

    private void ClearAllDrawings()
    {
        var drawingManager = FindFirstObjectByType<ARDrawingManager>();
        if (drawingManager != null)
        {
            drawingManager.ClearAllLines();
            Debug.Log("[UIFlow] Clear all drawings requested");

            PerformanceDiagnostics.Instance?.LogDrawingLocal("CLEAR ALL requested by user");
        }
        else
        {
            Debug.LogError("[UIFlow] ARDrawingManager not found!");
            PerformanceDiagnostics.Instance?.LogSystem("ARDrawingManager not found!");
        }
    }

    private void OnWidthChanged(float value)
    {
        var drawingManager = FindFirstObjectByType<ARDrawingManager>();
        if (drawingManager != null)
        {
            drawingManager.SetLineWidth(value);
        }
        UpdateWidthText(value);
    }

    private void OnDistanceChanged(float value)
    {
        var drawingManager = FindFirstObjectByType<ARDrawingManager>();
        if (drawingManager != null)
        {
            drawingManager.SetDrawingDistance(value);
        }
        UpdateDistanceText(value);
    }

    private void UpdateWidthText(float value)
    {
        if (widthValueText != null)
        {
            widthValueText.text = $"Line Width: {value:F3}";
        }
    }

    private void UpdateDistanceText(float value)
    {
        if (distanceValueText != null)
        {
            distanceValueText.text = $"Draw Distance: {value:F1}m";
        }
    }

    #endregion


    #region debug panel

    private void ToggleDebugPanel()
    {
        if (PerformanceDiagnostics.Instance != null)
        {
            PerformanceDiagnostics.Instance.ToggleDiagnostics();

            isDebugPanelVisible = PerformanceDiagnostics.Instance.IsDiagnosticsVisible();
            UpdateDebugButtonText();

            Debug.Log($"[UIFlow] Debug panel {(isDebugPanelVisible ? "shown" : "hidden")}");
        }
        else
        {
            Debug.LogWarning("[UIFlow] PerformanceDiagnostics not found in scene!");
            UpdateConnectionStatus("Debug manager not available");
        }
    }

    private void UpdateDebugButtonText()
    {
        if (debugButtonText != null)
        {
            debugButtonText.text = isDebugPanelVisible ? "Hide Debug" : "Show Debug";
        }

        if (debugToggleButton != null)
        {
            var buttonImage = debugToggleButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isDebugPanelVisible ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.2f, 0.2f, 0.2f);
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12) || Input.GetKeyDown(KeyCode.BackQuote))
        {
            if (PerformanceDiagnostics.Instance != null)
            {
                isDebugPanelVisible = PerformanceDiagnostics.Instance.IsDiagnosticsVisible();
                UpdateDebugButtonText();
            }
        }
    }


    #endregion


    #region Control Panel
    private void ToggleMuteAudio()
    {
        isAudioMuted = !isAudioMuted;

        if (AgoraManager.Instance != null)
        {
            AgoraManager.Instance.MuteLocalAudio(isAudioMuted);
        }

        if (muteAudioButton != null)
        {
            var buttonImage = muteAudioButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isAudioMuted ? Color.red : Color.white;
            }
        }

        Debug.Log($"[UIFlow] Audio {(isAudioMuted ? "muted" : "unmuted")}");

        PerformanceDiagnostics.Instance?.LogRTCEvent($"Audio {(isAudioMuted ? "Muted" : "Unmuted")}");
    }

    private void ToggleMuteVideo()
    {
        isVideoMuted = !isVideoMuted;

        if (AgoraManager.Instance != null)
        {
            AgoraManager.Instance.MuteLocalVideo(isVideoMuted);
        }

        if (muteVideoButton != null)
        {
            var buttonImage = muteVideoButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isVideoMuted ? Color.red : Color.white;
            }
        }

        Debug.Log($"[UIFlow] Video {(isVideoMuted ? "muted" : "unmuted")}");

        PerformanceDiagnostics.Instance?.LogRTCEvent($"Video {(isVideoMuted ? "Muted" : "Unmuted")}");
    }

    private void UpdateConnectionStatus(string status)
    {
        if (connectionStatusText)
        {
            connectionStatusText.text = status;
        }
        Debug.Log($"[UIFlow Status] {status}");
    }

    #endregion

    public bool IsTracking => isTracking;
    public bool IsCallActive => isCallActive;
    public Color CurrentDrawingColor => currentDrawingColor;
    public bool IsDebugPanelVisible => isDebugPanelVisible;
}