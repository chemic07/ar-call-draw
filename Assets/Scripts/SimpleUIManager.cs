using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple UI Manager for testing AR Video Call functionality
/// Provides buttons to join/leave channel and displays connection status
/// </summary>
public class SimpleUIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button joinButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Optional")]
    [SerializeField] private Button testRemoteViewButton; // For testing VideoSurface setup

    public static SimpleUIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        SetupButtons();
        UpdateStatus("Ready to join channel");
    }

    void SetupButtons()
    {
        if (joinButton != null)
        {
            joinButton.onClick.AddListener(OnJoinButtonClicked);
            joinButton.interactable = true;
        }

        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(OnLeaveButtonClicked);
            leaveButton.interactable = false;
        }

        if (testRemoteViewButton != null)
        {
            testRemoteViewButton.onClick.AddListener(OnTestRemoteView);
        }
    }

    void OnJoinButtonClicked()
    {
        Debug.Log("[UI] Join button clicked");

        if (AgoraManager.Instance != null)
        {
            AgoraManager.Instance.JoinChannel();
            UpdateStatus("Joining channel...");

            if (joinButton != null) joinButton.interactable = false;
            if (leaveButton != null) leaveButton.interactable = true;
        }
        else
        {
            Debug.LogError("AgoraManager instance not found!");
            UpdateStatus("ERROR: AgoraManager not found!");
        }
    }

    void OnLeaveButtonClicked()
    {
        Debug.Log("[UI] Leave button clicked");

        if (AgoraManager.Instance != null)
        {
            AgoraManager.Instance.LeaveChannel();
            UpdateStatus("Left channel");

            if (joinButton != null) joinButton.interactable = true;
            if (leaveButton != null) leaveButton.interactable = false;
        }
    }

    void OnTestRemoteView()
    {
        Debug.Log("[UI] Testing remote view setup...");

        // This is a debug function to manually trigger remote view setup
        // Useful for testing without needing a second device
        if (AgoraManager.Instance != null && AgoraManager.Instance.RemoteUID != 0)
        {
            ARScreenShareManager screenShare = FindObjectOfType<ARScreenShareManager>();
            if (screenShare != null)
            {
                screenShare.OnRemoteUserJoined(AgoraManager.Instance.RemoteUID);
                UpdateStatus($"Manually triggered remote setup for UID: {AgoraManager.Instance.RemoteUID}");
            }
        }
        else
        {
            UpdateStatus("No remote user found to test");
        }
    }

    public void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
            Debug.Log($"[UI Status] {message}");
        }
    }

    public void OnChannelJoined(string channelName, uint uid)
    {
        UpdateStatus($"Joined: {channelName} (UID: {uid})");
    }

    public void OnRemoteUserJoined()
    {
        UpdateStatus("Remote user joined - Setting up video...");
    }

    public void OnRemoteUserLeft()
    {
        UpdateStatus("Remote user left");
    }
}