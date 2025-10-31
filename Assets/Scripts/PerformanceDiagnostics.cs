using UnityEngine;
using TMPro;
using System.Text;
using System.Collections.Generic;
using System.Linq;

public class PerformanceDiagnostics : MonoBehaviour
{
    [Header("UI Reference (Assign your existing Text component)")]
    [SerializeField] private TextMeshProUGUI diagnosticsText;

    [Header("Settings")]
    [SerializeField] private float updateInterval = 0.5f;
    [SerializeField] private int maxLogsPerCategory = 10;

    [Header("Filters")]
    [SerializeField] private bool showPerformance = true;
    [SerializeField] private bool showRTM = true;
    [SerializeField] private bool showRTC = true;
    [SerializeField] private bool showDrawing = true;
    [SerializeField] private bool showSystem = true;

    // Singleton
    public static PerformanceDiagnostics Instance;

    // Manager references 
    private AgoraRTMSimpleTest rtmManager;
    private AgoraManager agoraManager;
    private ARDrawingManager drawingManager;

    // Message tracking
    private Queue<DebugMessage> rtmSentMessages = new Queue<DebugMessage>();
    private Queue<DebugMessage> rtmReceivedMessages = new Queue<DebugMessage>();
    private Queue<DebugMessage> rtcEvents = new Queue<DebugMessage>();
    private Queue<DebugMessage> drawingEvents = new Queue<DebugMessage>();
    private Queue<DebugMessage> systemEvents = new Queue<DebugMessage>();

    // Performance metrics
    private float fps;
    private int frameCount;
    private float deltaTime;
    private float lastUpdateTime;
    private long memoryUsage;

    // Statistics
    private int totalRTMSent = 0;
    private int totalRTMReceived = 0;
    private int totalDrawingPoints = 0;
    private int totalRemotePoints = 0;
    private int activeLinesLocal = 0;
    private int activeLinesRemote = 0;

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
        FindManagers();
        LogSystem("Performance Diagnostics initialized");
    }

    private void FindManagers()
    {
        rtmManager = FindFirstObjectByType<AgoraRTMSimpleTest>();
        agoraManager = FindFirstObjectByType<AgoraManager>();
        drawingManager = FindFirstObjectByType<ARDrawingManager>();
    }

    private void Update()
    {
        // Update FPS
        frameCount++;
        deltaTime += Time.unscaledDeltaTime;

        if (Time.time - lastUpdateTime >= updateInterval)
        {
            fps = frameCount / deltaTime;
            memoryUsage = System.GC.GetTotalMemory(false);

            if (diagnosticsText != null && diagnosticsText.gameObject.activeInHierarchy)
            {
                UpdateDiagnosticsDisplay();
            }

            frameCount = 0;
            deltaTime = 0;
            lastUpdateTime = Time.time;
        }
    }
    #region  agora 
    public void LogRTMSent(string lineId, bool isStart, Vector3 position, string userId)
    {
        totalRTMSent++;
        string shortId = lineId.Length >= 8 ? lineId.Substring(0, 8) : lineId;
        string msg = $"SENT | User:{userId} | Line:{shortId} | Start:{isStart} | Pos:{FormatVector(position)}";
        AddMessage(rtmSentMessages, new DebugMessage(msg, DebugCategory.RTM_SENT));
    }

    public void LogRTMReceived(string lineId, bool isStart, Vector3 position, string publisherId)
    {
        totalRTMReceived++;
        string shortId = lineId.Length >= 8 ? lineId.Substring(0, 8) : lineId;
        string msg = $"RECV | Pub:{publisherId} | Line:{shortId} | Start:{isStart} | Pos:{FormatVector(position)}";
        AddMessage(rtmReceivedMessages, new DebugMessage(msg, DebugCategory.RTM_RECEIVED));
    }

    public void LogRTMClearSent(string userId)
    {
        totalRTMSent++;
        string msg = $"CLEAR SENT | User:{userId}";
        AddMessage(rtmSentMessages, new DebugMessage(msg, DebugCategory.RTM_SENT, DebugSeverity.Warning));
    }

    public void LogRTMClearReceived(string publisherId)
    {
        totalRTMReceived++;
        string msg = $"CLEAR RECV | Pub:{publisherId}";
        AddMessage(rtmReceivedMessages, new DebugMessage(msg, DebugCategory.RTM_RECEIVED, DebugSeverity.Warning));
    }

    public void LogRTCEvent(string eventType, string details = "")
    {
        string msg = string.IsNullOrEmpty(details) ? eventType : $"{eventType} | {details}";
        AddMessage(rtcEvents, new DebugMessage(msg, DebugCategory.RTC));
    }


    #endregion

    #region  drawing
    public void LogDrawingLocal(string action, int pointCount = 0)
    {
        if (action.Contains("Point"))
            totalDrawingPoints++;

        string msg = $"LOCAL | {action}" + (pointCount > 0 ? $" | Points:{pointCount}" : "");
        AddMessage(drawingEvents, new DebugMessage(msg, DebugCategory.DRAWING));
    }

    public void LogDrawingRemote(string action, string publisherId, int pointCount = 0)
    {
        if (action.Contains("Point"))
            totalRemotePoints++;

        string msg = $"REMOTE | {action} | Pub:{publisherId}" + (pointCount > 0 ? $" | Points:{pointCount}" : "");
        AddMessage(drawingEvents, new DebugMessage(msg, DebugCategory.DRAWING));
    }

    public void LogSystem(string message, DebugSeverity severity = DebugSeverity.Info)
    {
        AddMessage(systemEvents, new DebugMessage(message, DebugCategory.SYSTEM, severity));
    }

    public void UpdateLineStats(int local, int remote)
    {
        activeLinesLocal = local;
        activeLinesRemote = remote;
    }


    #endregion

    #region  debug controller
    public void SetDiagnosticsText(TextMeshProUGUI textComponent)
    {
        diagnosticsText = textComponent;
    }

    public void ShowDiagnostics()
    {
        if (diagnosticsText != null)
        {
            diagnosticsText.gameObject.SetActive(true);
        }
    }
    public void HideDiagnostics()
    {
        if (diagnosticsText != null)
        {
            diagnosticsText.gameObject.SetActive(false);
        }
    }

    public void ToggleDiagnostics()
    {
        if (diagnosticsText != null)
        {
            diagnosticsText.gameObject.SetActive(!diagnosticsText.gameObject.activeInHierarchy);
        }
    }


    public void ClearAllLogs()
    {
        rtmSentMessages.Clear();
        rtmReceivedMessages.Clear();
        rtcEvents.Clear();
        drawingEvents.Clear();
        systemEvents.Clear();

        totalRTMSent = 0;
        totalRTMReceived = 0;
        totalDrawingPoints = 0;
        totalRemotePoints = 0;

        LogSystem("All logs cleared");
    }

    public bool IsDiagnosticsVisible()
    {
        return diagnosticsText != null && diagnosticsText.gameObject.activeInHierarchy;
    }

    private void UpdateDiagnosticsDisplay()
    {
        if (diagnosticsText == null) return;

        StringBuilder sb = new StringBuilder();

        // Header
        sb.AppendLine("<color=#FFD700>=== PERFORMANCE DIAGNOSTICS ===</color>");
        sb.AppendLine($"<color=#808080>Updated: {System.DateTime.Now:HH:mm:ss}</color>\n");

        // Performance Section
        if (showPerformance)
        {
            sb.AppendLine("<color=#00FF7F>▼ PERFORMANCE</color>");
            sb.AppendLine($"FPS: {fps:F1} {GetFPSStatus(fps)}");
            sb.AppendLine($"Memory: {memoryUsage / 1024 / 1024}MB");
            sb.AppendLine($"Frame: {(1000f / fps):F1}ms\n");
        }

        // Connection Status
        sb.AppendLine("<color=#00C8FF>▼ CONNECTION</color>");

        if (rtmManager != null)
        {
            string rtmStatus = rtmManager.IsLoggedIn ? "<color=#00FF00>✓</color>" : "<color=#FF3333>✗</color>";
            sb.AppendLine($"RTM: {rtmStatus} User:{rtmManager.UserId}");
        }
        else
        {
            sb.AppendLine($"RTM: <color=#808080>Not Found</color>");
        }

        if (agoraManager != null)
        {
            string rtcStatus = agoraManager.IsJoined ? "<color=#00FF00>✓</color>" : "<color=#FF3333>✗</color>";
            sb.AppendLine($"RTC: {rtcStatus} Ch:{agoraManager.ChannelName}");
            if (agoraManager.RemoteUID > 0)
                sb.AppendLine($"Remote: {agoraManager.RemoteUID}");
        }
        else
        {
            sb.AppendLine($"RTC: <color=#808080>Not Found</color>");
        }
        sb.AppendLine();

        #endregion

        // Statistics
        sb.AppendLine("<color=#FFB800>▼ STATS</color>");
        sb.AppendLine($"RTM Sent:{totalRTMSent} Recv:{totalRTMReceived}");
        sb.AppendLine($"Draw Local:{totalDrawingPoints} Remote:{totalRemotePoints}");
        sb.AppendLine($"Lines Local:{activeLinesLocal} Remote:{activeLinesRemote}\n");


        #region rtm rtc event
        // RTM Messages
        if (showRTM && (rtmSentMessages.Count > 0 || rtmReceivedMessages.Count > 0))
        {
            sb.AppendLine("<color=#00C8FF>▼ RTM (Recent)</color>");

            if (rtmSentMessages.Count > 0)
            {
                sb.AppendLine("<color=#FF6B6B>Sent:</color>");
                foreach (var msg in rtmSentMessages.Reverse().Take(maxLogsPerCategory))
                {
                    sb.AppendLine($"  {msg.GetFormattedMessage()}");
                }
            }

            if (rtmReceivedMessages.Count > 0)
            {
                sb.AppendLine("<color=#4ECDC4>Received:</color>");
                foreach (var msg in rtmReceivedMessages.Reverse().Take(maxLogsPerCategory))
                {
                    sb.AppendLine($"  {msg.GetFormattedMessage()}");
                }
            }
            sb.AppendLine();
        }



        // RTC Events
        if (showRTC && rtcEvents.Count > 0)
        {
            sb.AppendLine("<color=#9B59B6>▼ RTC EVENTS</color>");
            foreach (var msg in rtcEvents.Reverse().Take(maxLogsPerCategory))
            {
                sb.AppendLine($"{msg.GetFormattedMessage()}");
            }
            sb.AppendLine();
        }


        #endregion
        // Drawing Events
        if (showDrawing && drawingEvents.Count > 0)
        {
            sb.AppendLine("<color=#FFB800>▼ DRAWING</color>");
            foreach (var msg in drawingEvents.Reverse().Take(maxLogsPerCategory))
            {
                sb.AppendLine($"{msg.GetFormattedMessage()}");
            }
            sb.AppendLine();
        }
        #region  sys event
        // System Events
        if (showSystem && systemEvents.Count > 0)
        {
            sb.AppendLine("<color=#95A5A6>▼ SYSTEM</color>");
            foreach (var msg in systemEvents.Reverse().Take(maxLogsPerCategory))
            {
                sb.AppendLine($"{msg.GetFormattedMessage()}");
            }
        }

        diagnosticsText.text = sb.ToString();
    }


    #endregion


    private void AddMessage(Queue<DebugMessage> queue, DebugMessage message)
    {
        queue.Enqueue(message);

        if (queue.Count > maxLogsPerCategory * 2)
        {
            queue.Dequeue();
        }
    }

    private string GetFPSStatus(float currentFps)
    {
        if (currentFps < 15) return "<color=#FF3333>⚠ LAG</color>";
        if (currentFps < 24) return "<color=#FF8800>⚠ Poor</color>";
        if (currentFps < 30) return "<color=#FFFF00>OK</color>";
        return "<color=#00FF00>Good</color>";
    }

    private string FormatVector(Vector3 v)
    {
        return $"({v.x:F2},{v.y:F2},{v.z:F2})";
    }


    #region export report
    public string ExportDebugReport()
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("===== PERFORMANCE DIAGNOSTICS REPORT =====");
        report.AppendLine($"Timestamp: {System.DateTime.Now}");
        report.AppendLine($"FPS: {fps:F1}");
        report.AppendLine($"Memory: {memoryUsage / 1024 / 1024}MB");
        report.AppendLine();

        report.AppendLine("=== STATISTICS ===");
        report.AppendLine($"RTM Sent: {totalRTMSent}");
        report.AppendLine($"RTM Received: {totalRTMReceived}");
        report.AppendLine($"Drawing Points: {totalDrawingPoints}");
        report.AppendLine($"Remote Points: {totalRemotePoints}");
        report.AppendLine($"Active Lines (Local/Remote): {activeLinesLocal}/{activeLinesRemote}");
        report.AppendLine();

        report.AppendLine("=== RTM SENT MESSAGES ===");
        foreach (var msg in rtmSentMessages)
        {
            report.AppendLine(msg.GetFormattedMessage());
        }
        report.AppendLine();

        report.AppendLine("=== RTM RECEIVED MESSAGES ===");
        foreach (var msg in rtmReceivedMessages)
        {
            report.AppendLine(msg.GetFormattedMessage());
        }

        report.AppendLine();
        report.AppendLine("=== RTC EVENTS ===");
        foreach (var msg in rtcEvents)
        {
            report.AppendLine(msg.GetFormattedMessage());
        }

        report.AppendLine();
        report.AppendLine("=== DRAWING EVENTS ===");
        foreach (var msg in drawingEvents)
        {
            report.AppendLine(msg.GetFormattedMessage());
        }

        report.AppendLine();
        report.AppendLine("=== SYSTEM EVENTS ===");
        foreach (var msg in systemEvents)
        {
            report.AppendLine(msg.GetFormattedMessage());
        }

        return report.ToString();
    }

    #endregion
    // ==================== DATA STRUCTURES ====================
    #region debug message class
    private class DebugMessage
    {
        public string message;
        public DebugCategory category;
        public DebugSeverity severity;
        public float timestamp;

        public DebugMessage(string msg, DebugCategory cat, DebugSeverity sev = DebugSeverity.Info)
        {
            message = msg;
            category = cat;
            severity = sev;
            timestamp = Time.time;
        }

        public string GetFormattedMessage()
        {
            string timeStr = System.DateTime.Now.ToString("HH:mm:ss");
            string severityIcon = severity switch
            {
                DebugSeverity.Error => "<color=#FF3333>✗</color>",
                DebugSeverity.Warning => "<color=#FFFF00>⚠</color>",
                _ => "<color=#00FF00>•</color>"
            };

            return $"{severityIcon}[{timeStr}] {message}";
        }
    }
    #endregion

    #region debug category enum
    private enum DebugCategory
    {
        RTM_SENT,
        RTM_RECEIVED,
        RTC,
        DRAWING,
        SYSTEM
    }

    public enum DebugSeverity
    {
        Info,
        Warning,
        Error
    }
}
#endregion
