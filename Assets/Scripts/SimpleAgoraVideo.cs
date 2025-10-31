using UnityEngine;
using UnityEngine.UI;
using Agora.Rtc;

public class SimpleAgoraVideo : MonoBehaviour
{
    [Header("Agora Config")]
    [SerializeField] private string appId = "8eab2a20719a4f3ea082bf78049921b2";
    [SerializeField] private string token = "007eJxTYDDs4TyQfEo8cD2T5zIGlWjeP+ndey7t9Ho2zYajY22OX5YCg0VqYpJRopGBuaFlokmacWqigYVRUpq5hYGJpaWRYZJRUPqvjIZARgbGjv2sQBIMQXwehpLU4pL45IzEvLzUHAYGAKu5ILw=";
    [SerializeField] private string channelName = "test_Channel";

    [Header("Video Surfaces (Assign in Inspector)")]
    public RawImage localRawImage;
    public RawImage remoteRawImage;

    private IRtcEngine rtcEngine;
    private VideoSurface localSurface;
    private VideoSurface remoteSurface;

    void Start()
    {
        InitializeAgora();
    }

    private void InitializeAgora()
    {
        if (string.IsNullOrEmpty(appId))
        {
            Debug.LogError("App ID is missing! Please set your Agora App ID.");
            return;
        }

        rtcEngine = Agora.Rtc.RtcEngine.CreateAgoraRtcEngine();

        RtcEngineContext context = new RtcEngineContext
        {
            appId = appId,
            channelProfile = CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_COMMUNICATION,
            audioScenario = AUDIO_SCENARIO_TYPE.AUDIO_SCENARIO_DEFAULT
        };

        rtcEngine.Initialize(context);

        rtcEngine.EnableVideo();

        VideoEncoderConfiguration config = new VideoEncoderConfiguration
        {
            dimensions = new VideoDimensions(320, 240),
            frameRate = 15,
            bitrate = 0,
            minBitrate = 200,
            degradationPreference = DEGRADATION_PREFERENCE.MAINTAIN_FRAMERATE
        };

        rtcEngine.SetVideoEncoderConfiguration(config);
        rtcEngine.EnableDualStreamMode(true);

        InitEventHandler();

        SetupLocalVideo();
        JoinChannel();

        Debug.Log("Agora Engine initialized successfully!");
    }

    private void InitEventHandler()
    {
        rtcEngine.InitEventHandler(new UserEventHandler(this));
    }

    void SetupLocalVideo()
    {
        if (localRawImage == null)
        {
            Debug.LogWarning("Local RawImage not assigned!");
            return;
        }

        localSurface = localRawImage.gameObject.AddComponent<VideoSurface>();
        localSurface.SetForUser(0); // Local user
        localSurface.SetEnable(true);

    }

    void SetupRemoteVideo(uint uid)
    {
        if (remoteRawImage == null)
        {
            Debug.LogWarning("Remote RawImage not assigned!");
            return;
        }

        remoteSurface = remoteRawImage.gameObject.AddComponent<VideoSurface>();
        remoteSurface.SetForUser(uid);
        remoteSurface.SetEnable(true);
    }

    void JoinChannel()
    {
        rtcEngine.JoinChannel(token, channelName, "", 0);
        Debug.Log($"Joining channel: {channelName}");
    }

    public void LeaveChannel()
    {
        rtcEngine.LeaveChannel();
        Debug.Log("Left the channel");

        if (localSurface != null) localSurface.SetEnable(false);
        if (remoteSurface != null) remoteSurface.SetEnable(false);
    }

    void OnApplicationQuit()
    {
        if (rtcEngine != null)
        {
            rtcEngine.LeaveChannel();
            rtcEngine.Dispose();
            rtcEngine = null;
        }
    }

    internal class UserEventHandler : IRtcEngineEventHandler
    {
        private readonly SimpleAgoraVideo main;

        public UserEventHandler(SimpleAgoraVideo main)
        {
            this.main = main;
        }

        public override void OnJoinChannelSuccess(RtcConnection connection, int elapsed)
        {
            Debug.Log($"✅ Joined channel: {connection.channelId}");
        }

        public override void OnUserJoined(RtcConnection connection, uint uid, int elapsed)
        {
            Debug.Log($"👤 Remote user joined: {uid}");
            main.SetupRemoteVideo(uid);
        }

        public override void OnUserOffline(RtcConnection connection, uint uid, USER_OFFLINE_REASON_TYPE reason)
        {
            Debug.Log($"❌ Remote user left: {uid}");
            if (main.remoteSurface != null)
                main.remoteSurface.SetEnable(false);
        }
    }
}
