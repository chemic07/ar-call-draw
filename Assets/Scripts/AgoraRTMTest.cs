using UnityEngine;
using Agora.Rtm;
using System;
using System.Threading.Tasks;

public class AgoraRTMSimpleTest : MonoBehaviour
{
    [Header("Agora RTM Settings")]
    [SerializeField] private string appId = "YOUR_APP_ID";
    [SerializeField] private string rtmToken = ""; // ✅ RTM Token (different from RTC)
    [SerializeField] private string channelName = "test_channel";
    [SerializeField] private string customUserId = "";

    [Header("Drawing Manager Dependency")]
    [SerializeField] private ARDrawingManager arDrawingManager;

    private string userId;
    private IRtmClient rtmClient;
    private bool isLoggedIn = false;
    private bool credentialsSet = false;

    public string UserId => userId;
    public bool IsLoggedIn => isLoggedIn;

    private void Start()
    {
        if (arDrawingManager == null)
        {
            Debug.LogWarning("⚠️ ARDrawingManager is not assigned. Will try to find it...");
            arDrawingManager = FindFirstObjectByType<ARDrawingManager>();
        }

        Debug.Log("[RTM] Waiting for credentials from UI...");
    }
    public async void SetCredentials(string newUserId, string newChannelName, string newRtmToken = "")
    {
        if (credentialsSet)
        {
            Debug.LogWarning("[RTM] Credentials already set. Ignoring duplicate call.");
            return;
        }

        customUserId = newUserId;
        channelName = newChannelName;
        rtmToken = newRtmToken;

        credentialsSet = true;

        Debug.Log($"[RTM] Credentials set: User={newUserId}, Channel={newChannelName}, Token={(string.IsNullOrEmpty(newRtmToken) ? "None" : "Present")}");

        await InitializeRTM();
    }


    #region RTM Init
    private async Task InitializeRTM()
    {
        if (string.IsNullOrEmpty(customUserId))
        {
            userId = Guid.NewGuid().ToString().Substring(0, 8);
        }
        else
        {
            userId = customUserId;
        }
        Debug.Log($"<color=lime>🟢 RTM Initializing for user:</color> <color=yellow>{userId}</color>");

        if (string.IsNullOrEmpty(appId) || appId == "YOUR_APP_ID")
        {
            Debug.LogError("❌ App ID is required to initialize RTM!");
            return;
        }

        RtmLogConfig logConfig = new RtmLogConfig
        {
            filePath = Application.persistentDataPath + "/agora_rtm.log",
            fileSizeInKB = 512,
            level = RTM_LOG_LEVEL.INFO
        };

        RtmConfig config = new RtmConfig
        {
            appId = appId,
            userId = userId,
            logConfig = logConfig
        };

        try
        {
            rtmClient = RtmClient.CreateAgoraRtmClient(config);
            Debug.Log("<color=lime>✅ RTM Client created successfully!</color>");
        }
        catch (RTMException e)
        {
            Debug.LogError($"❌ RTM Client creation failed: {e.Status.ErrorCode} - {e.Status.Reason}");
            return;
        }

        rtmClient.OnMessageEvent += OnRtmMessageEvent;
        rtmClient.OnConnectionStateChanged += OnRtmConnectionStateChanged;

        // login rtm
        var loginResult = await rtmClient.LoginAsync(rtmToken);
        var status = loginResult.Status;
        if (status.Error)
        {
            Debug.LogError($"❌ RTM Login failed: {status.ErrorCode} - {status.Reason}");
            return;
        }
        Debug.Log($"<color=lime>✅ RTM Login successful for user</color> <color=yellow>{userId}</color>");
        isLoggedIn = true;

        SubscribeOptions options = new SubscribeOptions
        {
            withMessage = true,
            withPresence = true
        };

        var subResult = await rtmClient.SubscribeAsync(channelName, options);
        var subStatus = subResult.Status;
        if (subStatus.Error)
        {
            Debug.LogError($"❌ Failed to join RTM channel: {subStatus.ErrorCode} - {subStatus.Reason}");
        }
        else
        {
            Debug.Log($"<color=cyan>🎯 Joined RTM channel:</color> <color=yellow>{channelName}</color>");
        }
    }
    #endregion

    #region RTM Event Handlers
    private void OnRtmMessageEvent(MessageEvent e)
    {
        if (e.channelName == channelName && e.messageType == RTM_MESSAGE_TYPE.STRING)
        {
            string jsonMsg = e.message.GetData<string>();

            try
            {
                if (jsonMsg.Contains("\"command\""))
                {
                    ClearCommand clearCmd = JsonUtility.FromJson<ClearCommand>(jsonMsg);

                    if (clearCmd.command == "CLEAR_ALL" && clearCmd.publisherId != userId)
                    {
                        Debug.Log($"<color=orange>🗑️ CLEAR COMMAND</color> | From: <color=yellow>{clearCmd.publisherId}</color>");

                        if (arDrawingManager != null)
                        {
                            arDrawingManager.HandleRemoteClearCommand(clearCmd);
                        }
                    }
                }
                else
                {
                    DrawingData remoteData = JsonUtility.FromJson<DrawingData>(jsonMsg);

                    if (remoteData.publisherId != userId)
                    {
                        if (arDrawingManager != null)
                        {
                            arDrawingManager.HandleRemoteDrawingData(remoteData);
                        }

                        string lineIdShort = remoteData.lineId.Length >= 4
                            ? remoteData.lineId.Substring(0, 4)
                            : remoteData.lineId;
                        Debug.Log($"<color=blue>💬 RECEIVED</color> | Pub: <color=yellow>{e.publisher}</color> | Line: <color=yellow>{lineIdShort}</color> | Start: {remoteData.isStart} | Pos: ({remoteData.pX:F2}, {remoteData.pY:F2}, {remoteData.pZ:F2})");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Failed to parse RTM message: {ex.Message}. Raw: {jsonMsg}");
            }
        }
    }

    private void OnRtmConnectionStateChanged(string channel, RTM_CONNECTION_STATE state, RTM_CONNECTION_CHANGE_REASON reason)
    {
        Debug.Log($"🔄 Connection state changed: <color=orange>{state}</color>, Reason: {reason}");

        if (state == RTM_CONNECTION_STATE.FAILED || state == RTM_CONNECTION_STATE.DISCONNECTED)
        {
            isLoggedIn = false;
        }
        else if (state == RTM_CONNECTION_STATE.CONNECTED)
        {
            isLoggedIn = true;
        }
    }

    #endregion

    #region Send Meaagage
    public async void SendRtmMessage(string messageJson)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("⚠️ Cannot send RTM message: Not logged in.");
            return;
        }

        try
        {
            if (messageJson.Contains("\"command\""))
            {
                ClearCommand cmd = JsonUtility.FromJson<ClearCommand>(messageJson);
                Debug.Log($"<color=red>🗑️ SENDING CLEAR</color> | User: <color=yellow>{userId}</color>");
            }
            else
            {
                DrawingData data = JsonUtility.FromJson<DrawingData>(messageJson);
                string lineIdShort = data.lineId.Length >= 4
                    ? data.lineId.Substring(0, 4)
                    : data.lineId;
                Debug.Log($"<color=red>📨 SENT</color> | User: <color=yellow>{userId}</color> | Line: <color=yellow>{lineIdShort}</color> | Start: {data.isStart} | Pos: ({data.pX:F2}, {data.pY:F2}, {data.pZ:F2})");
            }

            PublishOptions publishOptions = new PublishOptions
            {
                channelType = RTM_CHANNEL_TYPE.MESSAGE,
                customType = messageJson.Contains("\"command\"") ? "ClearCommand" : "DrawingData"
            };

            var sendResult = await rtmClient.PublishAsync(channelName, messageJson, publishOptions);
            var sendStatus = sendResult.Status;

            if (sendStatus.Error)
            {
                Debug.LogError($"❌ Failed to send message: {sendStatus.ErrorCode} - {sendStatus.Reason}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Error sending RTM message: {ex.Message}");
        }
    }


    #endregion

    #region Cleanup

    private async void OnApplicationQuit()
    {
        if (rtmClient != null)
        {
            rtmClient.OnMessageEvent -= OnRtmMessageEvent;
            rtmClient.OnConnectionStateChanged -= OnRtmConnectionStateChanged;

            if (isLoggedIn)
            {
                await rtmClient.LogoutAsync();
                Debug.Log("👋 Logged out from RTM.");
            }
            rtmClient.Dispose();
            Debug.Log("🧹 RTM client disposed.");
        }
    }
}

#endregion