using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using System.Linq;
using System;

public class ARDrawingManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private AgoraRTMSimpleTest rtmTest;
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Camera arCamera;

    [Header("Drawing Settings")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color currentColor = Color.red;


    public float lineWidth = 0.01f;
    public float editorDrawingDistance = 1.5f;
    public float minLineWidth = 0.005f;
    public float maxLineWidth = 0.05f;
    public float minDistance = 0.5f;
    public float maxDistance = 3.0f;


    private LineRenderer currentLineRenderer;
    private List<Vector3> currentLinePoints = new List<Vector3>();
    private string currentLineId;
    private bool isDrawing = false;
    private List<ARRaycastHit> hits = new List<ARRaycastHit>();


    private Dictionary<string, LineRenderer> remoteLines = new Dictionary<string, LineRenderer>();
    private List<GameObject> allLineObjects = new List<GameObject>();


    private bool hasDetectedPlane = false;
    private float planeDetectionTime = 0f;
    private const float PLANE_DETECTION_THRESHOLD = 1f;

    private void Awake()
    {
        if (rtmTest == null || arCamera == null || lineMaterial == null)
        {
            Debug.LogError("missing ardrawmanager ");
        }

        if (planeManager != null)
        {
            planeManager.planesChanged += OnPlanesChanged;
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
        HandleDrawingInputEditor();
#else
        HandleDrawingInputAR();
        CheckPlaneTracking();
#endif
    }


    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        if (!hasDetectedPlane && (args.added.Count > 0 || args.updated.Count > 0))
        {
            planeDetectionTime += Time.deltaTime;

            if (planeDetectionTime >= PLANE_DETECTION_THRESHOLD)
            {
                hasDetectedPlane = true;

                // Notify UI that tracking has started
                if (UIFlowManager.Instance != null)
                {
                    UIFlowManager.Instance.OnTrackingStarted();
                }

                Debug.Log("[Plane tracking established");
            }
        }
    }

    private void CheckPlaneTracking()
    {
        if (planeManager == null) return;

        bool hasActivePlanes = false;
        foreach (var plane in planeManager.trackables)
        {
            if (plane.trackingState == TrackingState.Tracking)
            {
                hasActivePlanes = true;
                break;
            }
        }

        if (hasDetectedPlane && !hasActivePlanes)
        {
            hasDetectedPlane = false;
            planeDetectionTime = 0f;

            if (UIFlowManager.Instance != null)
            {
                UIFlowManager.Instance.OnTrackingLost();
            }

            Debug.LogWarning("Tracking lost");
        }
    }


    #region input Handling

    private void HandleDrawingInputAR()
    {
        if (Input.touchCount > 0 && hasDetectedPlane)
        {
            Touch touch = Input.GetTouch(0);

            // Ignore touches 
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                return;

            Ray ray = arCamera.ScreenPointToRay(touch.position);

            // Draw at fixed distance from cam
            Vector3 drawPosition = ray.GetPoint(editorDrawingDistance);

            if (touch.phase == TouchPhase.Began)
            {
                StartLine(drawPosition);
            }
            else if (touch.phase == TouchPhase.Moved && isDrawing)
            {
                DrawLine(drawPosition);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                EndLine();
            }
        }
    }

    private void HandleDrawingInputEditor()
    {
        // Ignore clicks
        if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButton(0))
        {
            Ray ray = arCamera.ScreenPointToRay(Input.mousePosition);
            Vector3 drawPosition = ray.GetPoint(editorDrawingDistance);

            if (Input.GetMouseButtonDown(0))
            {
                StartLine(drawPosition);
            }
            else if (isDrawing)
            {
                DrawLine(drawPosition);
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            EndLine();
        }
    }

    #endregion

    #region Color Management

    public void SetDrawingColor(Color newColor)
    {
        currentColor = newColor;
        Debug.Log($"[ARDrawing] Color changed to: {GetColorName(newColor)}");
    }

    private string GetColorName(Color color)
    {
        if (color == Color.red) return "Red";
        if (color == Color.green) return "Green";
        if (color == Color.blue) return "Blue";
        return $"RGB({color.r:F2}, {color.g:F2}, {color.b:F2})";

    }

    #endregion
    #region Line Width & Distance
    public void SetLineWidth(float width)
    {
        lineWidth = Mathf.Clamp(width, minLineWidth, maxLineWidth);
        Debug.Log($"[ARDrawing] Line width set to: {lineWidth:F3}");
    }

    public void SetDrawingDistance(float distance)
    {
        editorDrawingDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        Debug.Log($"[ARDrawing] Drawing distance set to: {editorDrawingDistance:F2}");
    }

    #endregion

    #region Line Start
    private void StartLine(Vector3 position)
    {
        isDrawing = true;
        currentLineId = System.Guid.NewGuid().ToString();

        GameObject lineGO = new GameObject($"Line_{currentLineId}");
        currentLineRenderer = lineGO.AddComponent<LineRenderer>();

        // Create material instance with current color
        Material lineMat = new Material(lineMaterial);
        lineMat.color = currentColor;
        currentLineRenderer.material = lineMat;

        currentLineRenderer.startWidth = lineWidth;
        currentLineRenderer.endWidth = lineWidth;
        currentLineRenderer.positionCount = 1;
        currentLineRenderer.startColor = currentColor;
        currentLineRenderer.endColor = currentColor;

        currentLinePoints.Clear();
        currentLinePoints.Add(position);
        currentLineRenderer.SetPosition(0, position);

        allLineObjects.Add(lineGO);

        PerformanceDiagnostics.Instance?.LogDrawingLocal("Line Started");

        SendDrawingPoint(position, true);
    }

    #endregion

    #region draw line
    private void DrawLine(Vector3 position)
    {
        if (!isDrawing) return;

        if (currentLinePoints.Count == 0 || Vector3.Distance(currentLinePoints.Last(), position) > 0.01f)
        {
            currentLinePoints.Add(position);
            currentLineRenderer.positionCount = currentLinePoints.Count;
            currentLineRenderer.SetPosition(currentLinePoints.Count - 1, position);

            SendDrawingPoint(position, false);
        }

        PerformanceDiagnostics.Instance?.LogDrawingLocal("Point Added");

    }

    #endregion

    private void EndLine()
    {
        if (!isDrawing) return;
        isDrawing = false;
        Debug.Log($"[ARDrawing] Line completed: {currentLinePoints.Count} points");
    }


    #region clear lines
    public void ClearAllLines()
    {
        // Clear local lines
        foreach (var lineObj in allLineObjects)
        {
            if (lineObj != null)
                Destroy(lineObj);
        }
        allLineObjects.Clear();

        // Clear remote lines
        foreach (var kvp in remoteLines)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value.gameObject);
        }
        remoteLines.Clear();

        // Send clear message to other users

        PerformanceDiagnostics.Instance?.LogDrawingLocal("All Lines Cleared");
        SendClearMessage();

        Debug.Log("[ARDrawing] ✓ All drawings cleared and message sent");
    }

    public void ClearLocalLines()
    {
        foreach (var lineObj in allLineObjects)
        {
            if (lineObj != null)
                Destroy(lineObj);
        }
        allLineObjects.Clear();

        Debug.Log("[ARDrawing] ✓ Local drawings cleared");
    }

    private void SendClearMessage()
    {
        if (rtmTest != null && rtmTest.IsLoggedIn)
        {
            // Create a special clear command message
            ClearCommand clearCmd = new ClearCommand
            {
                command = "CLEAR_ALL",
                publisherId = rtmTest.UserId,
                timestamp = System.DateTime.UtcNow.Ticks
            };

            string jsonMessage = JsonUtility.ToJson(clearCmd);
            rtmTest.SendRtmMessage(jsonMessage);

            Debug.Log($"[ARDrawing] Clear command sent by {rtmTest.UserId}");
        }
    }

    #endregion

    #region Send Drawing Point
    private void SendDrawingPoint(Vector3 position, bool isStart)
    {
        if (rtmTest != null && rtmTest.IsLoggedIn)
        {
            DrawingData data = new DrawingData(
                currentLineId,
                position,
                currentColor,
                isStart,
                rtmTest.UserId
            );

            string jsonMessage = JsonUtility.ToJson(data);
            rtmTest.SendRtmMessage(jsonMessage);

            PerformanceDiagnostics.Instance?.LogRTMSent(currentLineId, isStart, position, rtmTest.UserId);
        }


    }

    public void HandleRemoteDrawingData(DrawingData data)
    {
        if (data.publisherId == rtmTest.UserId) return;


        PerformanceDiagnostics.Instance?.LogRTMReceived(data.lineId, data.isStart, data.GetPosition(), data.publisherId);

        if (data.isStart)
        {
            StartRemoteLine(data);
        }
        else
        {
            ContinueRemoteLine(data);
        }
    }

    public void HandleRemoteClearCommand(ClearCommand clearCmd)
    {
        if (clearCmd.publisherId == rtmTest.UserId) return;

        PerformanceDiagnostics.Instance?.LogRTMClearReceived(clearCmd.publisherId);

        Debug.Log($"[ARDrawing] Received clear command from {clearCmd.publisherId}");

        // Clear only remote lines (keep local lines)
        foreach (var kvp in remoteLines)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value.gameObject);
        }
        remoteLines.Clear();

        Debug.Log("[ARDrawing] ✓ Remote drawings cleared");
    }

    private void StartRemoteLine(DrawingData data)
    {
        if (remoteLines.ContainsKey(data.lineId))
        {
            Destroy(remoteLines[data.lineId].gameObject);
            remoteLines.Remove(data.lineId);
        }

        GameObject lineGO = new GameObject($"RemoteLine_{data.lineId}_{data.publisherId}");
        LineRenderer lr = lineGO.AddComponent<LineRenderer>();

        Color remoteColor = data.GetColor();

        Material lineMat = new Material(lineMaterial);
        lineMat.color = remoteColor;
        lr.material = lineMat;

        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.positionCount = 1;
        lr.startColor = remoteColor;
        lr.endColor = remoteColor;

        lr.SetPosition(0, data.GetPosition());
        remoteLines.Add(data.lineId, lr);

        Debug.Log($"[ARDrawing] Remote line started: {data.lineId.Substring(0, 4)} by {data.publisherId}");
    }

    private void ContinueRemoteLine(DrawingData data)
    {
        if (remoteLines.TryGetValue(data.lineId, out LineRenderer lr))
        {
            Vector3 position = data.GetPosition();
            lr.positionCount++;
            lr.SetPosition(lr.positionCount - 1, position);
        }
        else
        {
            Debug.LogWarning($"[ARDrawing] Point received for unknown line: {data.lineId.Substring(0, 4)}");
        }
    }


    #endregion

    private void OnDestroy()
    {
        if (planeManager != null)
        {
            planeManager.planesChanged -= OnPlanesChanged;
        }

        ClearAllLines();
    }
}

#region Common Data Classes

[Serializable]
public class ClearCommand
{
    public string command;
    public string publisherId;
    public long timestamp;
}

#endregion