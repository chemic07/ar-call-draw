using UnityEngine;
using Agora.Rtc;
using System.Collections;

public class AgoraManager : MonoBehaviour
{
    [Header("Agora Credentials")]
    [SerializeField] private string appID = "app-id";

    private string rtcToken = "";
    private string channelName = "";
    private string userId = "";

    [Header("AR Screen Share")]
    [SerializeField] private ARScreenShareManager screenShareManager;

    private IRtcEngine rtcEngine;
    private uint remoteUID = 0;
    private uint localUID = 0;
    private bool isJoined = false;
    private bool isInitialized = false;

    public static AgoraManager Instance { get; private set; }


    #region  init
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Debug.Log("[Agora RTC] Waiting for credentials from UI...");
    }

    public void SetCredentials(string newUserId, string newChannelName, string newRtcToken = "")
    {
        userId = newUserId;
        channelName = newChannelName;
        rtcToken = newRtcToken;

        Debug.Log($"[Agora RTC] Credentials set: User={newUserId}, Channel={newChannelName}, Token={(string.IsNullOrEmpty(newRtcToken) ? "None" : "Present")}");

        if (!isInitialized)
        {
            SetupAgoraEngine();
        }
    }

    private void SetupAgoraEngine()
    {
        if (string.IsNullOrEmpty(appID) || appID == "YOUR_APP_ID_HERE")
        {
            Debug.LogError("[Agora RTC] App ID missing!");
            return;
        }

        rtcEngine = Agora.Rtc.RtcEngine.CreateAgoraRtcEngine();

        RtcEngineContext context = new RtcEngineContext
        {
            appId = appID,
            channelProfile = CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_COMMUNICATION,
            audioScenario = AUDIO_SCENARIO_TYPE.AUDIO_SCENARIO_DEFAULT
        };

        int initResult = rtcEngine.Initialize(context);
        if (initResult != 0)
        {
            Debug.LogError($"[Agora RTC] Init failed: {initResult}");
            return;
        }

        rtcEngine.EnableVideo();
        rtcEngine.SetExternalVideoSource(true, false, EXTERNAL_VIDEO_SOURCE_TYPE.VIDEO_FRAME, new SenderOptions());

        int width = 960;
        int height = 540;

        if (screenShareManager != null && screenShareManager.arCamera != null)
        {
            Camera arCam = screenShareManager.arCamera;
            width = 960;
            height = Mathf.RoundToInt(width / arCam.aspect);
        }

        VideoEncoderConfiguration config = new VideoEncoderConfiguration
        {
            dimensions = new VideoDimensions(width, height),
            frameRate = 20,
            bitrate = 1200,
            minBitrate = 400,
            orientationMode = ORIENTATION_MODE.ORIENTATION_MODE_FIXED_PORTRAIT,
            degradationPreference = DEGRADATION_PREFERENCE.MAINTAIN_FRAMERATE,
            mirrorMode = VIDEO_MIRROR_MODE_TYPE.VIDEO_MIRROR_MODE_DISABLED
        };

        int configResult = rtcEngine.SetVideoEncoderConfiguration(config);
        if (configResult != 0)
            Debug.LogWarning($"[Agora RTC] SetVideoEncoderConfiguration warning: {configResult}");

        rtcEngine.EnableDualStreamMode(true);
        InitEventHandler();

        isInitialized = true;
        Debug.Log($"[Agora RTC] ✓ Initialized: {width}x{height} @ 20fps");
    }


    #endregion
    private void InitEventHandler()
    {
        UserEventHandler handler = new UserEventHandler(this);
        rtcEngine.InitEventHandler(handler);
    }
    #region  join leave
    public void JoinChannel(string channel = "")
    {
        if (!string.IsNullOrEmpty(channel))
        {
            channelName = channel;
        }

        if (string.IsNullOrEmpty(channelName))
        {
            Debug.LogError("[Agora RTC] Channel name not set! Call SetCredentials first.");
            return;
        }

        if (rtcEngine == null || !isInitialized)
        {
            Debug.LogError("[Agora RTC] RTC Engine not initialized! Call SetCredentials first.");
            return;
        }

        ChannelMediaOptions options = new ChannelMediaOptions();
        options.autoSubscribeAudio.SetValue(true);
        options.autoSubscribeVideo.SetValue(true);
        options.publishCameraTrack.SetValue(false);
        options.publishCustomVideoTrack.SetValue(true);
        options.publishMicrophoneTrack.SetValue(true);
        options.clientRoleType.SetValue(CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER);
        options.enableAudioRecordingOrPlayout.SetValue(true);

        int result = rtcEngine.JoinChannel(rtcToken, channelName, 0, options);

        if (result == 0)
        {
            Debug.Log($"[Agora RTC] Joining: {channelName} as user: {userId}");
        }
        else
        {
            Debug.LogError($"[Agora RTC] Join failed: {result}");
        }
    }

    public void LeaveChannel()
    {
        if (rtcEngine != null && isJoined)
        {
            if (screenShareManager != null)
            {
                screenShareManager.StopScreenSharing();
            }

            rtcEngine.LeaveChannel();
            isJoined = false;
            remoteUID = 0;
            localUID = 0;
            Debug.Log("[Agora RTC] Left channel");
        }
    }

    #endregion
    #region  video control
    public void MuteLocalAudio(bool mute)
    {
        rtcEngine?.MuteLocalAudioStream(mute);
        Debug.Log($"[Agora RTC] Audio muted: {mute}");
    }

    public void MuteLocalVideo(bool mute)
    {
        rtcEngine?.MuteLocalVideoStream(mute);

        if (mute)
        {
            screenShareManager?.StopScreenSharing();
        }
        else if (isJoined)
        {
            screenShareManager?.StartScreenSharing();
        }

        Debug.Log($"[Agora RTC] Video muted: {mute}");
    }

    #endregion

    public IRtcEngine GetRtcEngine()
    {
        return rtcEngine;
    }

    private void OnDestroy()
    {
        if (rtcEngine != null)
        {
            LeaveChannel();
            rtcEngine.Dispose();
            rtcEngine = null;
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (rtcEngine != null && isJoined)
        {
            if (pause)
            {
                Debug.Log("[Agora RTC] App paused");
                rtcEngine.MuteLocalVideoStream(true);
                screenShareManager?.StopScreenSharing();
            }
            else
            {
                Debug.Log("[Agora RTC] App resumed");
                rtcEngine.MuteLocalVideoStream(false);
                screenShareManager?.StartScreenSharing();
            }
        }
    }
    #region  event

    internal class UserEventHandler : IRtcEngineEventHandler
    {
        private readonly AgoraManager manager;

        internal UserEventHandler(AgoraManager manager)
        {
            this.manager = manager;
        }

        public override void OnJoinChannelSuccess(RtcConnection connection, int elapsed)
        {
            manager.localUID = connection.localUid;
            Debug.Log($"[Agora RTC] ✓ Joined: {connection.channelId}, UID: {connection.localUid}, User: {manager.userId}");
            manager.isJoined = true;

            PerformanceDiagnostics.Instance?.LogRTCEvent("Channel Joined", $"UID: {connection.localUid}, User: {manager.userId}");

            if (manager.screenShareManager != null)
            {
                manager.StartCoroutine(manager.StartSharingDelayed());
            }
        }

        public override void OnUserJoined(RtcConnection connection, uint uid, int elapsed)
        {
            Debug.Log($"[Agora RTC] ✓ Remote user joined: UID={uid}");
            manager.remoteUID = uid;

            PerformanceDiagnostics.Instance?.LogRTCEvent("User Joined", $"UID: {uid}");

            manager.rtcEngine.SetRemoteVideoStreamType(uid, VIDEO_STREAM_TYPE.VIDEO_STREAM_LOW);

            if (SimpleUIManager.Instance != null)
            {
                SimpleUIManager.Instance.OnRemoteUserJoined();
            }

            if (manager.screenShareManager != null)
            {
                manager.StartCoroutine(manager.DelayedRemoteSetup(uid));
            }
        }

        public override void OnUserOffline(RtcConnection connection, uint uid, USER_OFFLINE_REASON_TYPE reason)
        {
            Debug.Log($"[Agora RTC] User left: {uid} ({reason})");
            manager.remoteUID = 0;

            manager.screenShareManager?.OnRemoteUserLeft();
            SimpleUIManager.Instance?.OnRemoteUserLeft();
        }

        public override void OnFirstRemoteVideoFrame(RtcConnection connection, uint uid, int width, int height, int elapsed)
        {
            Debug.Log($"[Agora RTC] ✓ First frame: {uid}, {width}x{height}, delay: {elapsed}ms");
        }


        #endregion
        #region state change
        public override void OnRemoteVideoStateChanged(RtcConnection connection, uint uid,
            REMOTE_VIDEO_STATE state, REMOTE_VIDEO_STATE_REASON reason, int elapsed)
        {
            Debug.Log($"[Agora RTC] Remote video: {state} ({reason})");

            if (state == REMOTE_VIDEO_STATE.REMOTE_VIDEO_STATE_DECODING)
            {
                Debug.Log("[Agora RTC] ✓ Remote video decoding");
            }
            else if (state == REMOTE_VIDEO_STATE.REMOTE_VIDEO_STATE_FROZEN)
            {
                Debug.LogWarning($"[Agora RTC] Video frozen: {reason}");
            }
            else if (state == REMOTE_VIDEO_STATE.REMOTE_VIDEO_STATE_FAILED)
            {
                Debug.LogError($"[Agora RTC] Video failed: {reason}");
            }

            if (state == REMOTE_VIDEO_STATE.REMOTE_VIDEO_STATE_FAILED)
            {
                PerformanceDiagnostics.Instance?.LogRTCEvent("Video Failed", reason.ToString());
            }
            else if (state == REMOTE_VIDEO_STATE.REMOTE_VIDEO_STATE_FROZEN)
            {
                PerformanceDiagnostics.Instance?.LogRTCEvent("Video Frozen", reason.ToString());
            }
            else
            {
                PerformanceDiagnostics.Instance?.LogRTCEvent("Video State Changed", $"{state} | {reason}");
            }
        }

        public override void OnError(int error, string msg)
        {
            Debug.LogError($"[Agora RTC] Error {error}: {msg}");
        }

        public override void OnConnectionStateChanged(RtcConnection connection,
            CONNECTION_STATE_TYPE state, CONNECTION_CHANGED_REASON_TYPE reason)
        {
            Debug.Log($"[Agora RTC] Connection: {state} ({reason})");

            if (state == CONNECTION_STATE_TYPE.CONNECTION_STATE_FAILED)
            {
                Debug.LogError("[Agora RTC] Connection failed!");
            }
        }


        #endregion

        public override void OnNetworkQuality(RtcConnection connection, uint remoteUid,
            int txQuality, int rxQuality)
        {
            if (txQuality >= 4 || rxQuality >= 4)
            {
                Debug.LogWarning($"[Agora RTC] Poor network - TX:{txQuality}/6 RX:{rxQuality}/6");
            }
        }
    }

    private IEnumerator StartSharingDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        screenShareManager?.StartScreenSharing();
        Debug.Log("[Agora RTC] Screen sharing started");
    }

    private IEnumerator DelayedRemoteSetup(uint uid)
    {
        yield return new WaitForSeconds(0.5f);
        screenShareManager?.OnRemoteUserJoined(uid);
    }

    // Public properties
    public bool IsJoined => isJoined;
    public bool IsInitialized => isInitialized;
    public uint RemoteUID => remoteUID;
    public uint LocalUID => localUID;
    public string ChannelName => channelName;
    public string UserId => userId;
}