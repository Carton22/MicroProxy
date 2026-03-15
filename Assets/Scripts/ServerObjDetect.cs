// Modified for server-based YOLO inference
// @Carton 03/11/2026
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Meta.XR;
using Meta.XR.Samples;
using TMPro;
using PassthroughCameraSamples;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    public class ServerObjDetector : MonoBehaviour
    {
        [Header("Server Inference Config")]
        public string serverUrl = "http://10.131.93.214:8000/detect";
        public string healthUrl = "http://10.131.93.214:8000/health";
        public Camera passthroughCamera;

        [Header("Detection UI")]
        [SerializeField] private SentisInferenceUiManager m_uiInference;
        [SerializeField] private TextAsset m_labelsAsset;
        [SerializeField] private DetectionUiMenuManager m_uiMenuManager;
        [SerializeField] private ProxyInject m_proxyInject;
        [SerializeField] private ProxyCreator m_proxyCreator;
        [Tooltip("Optional: draw 2D bounding boxes in screen space (no 3D raycast).")]
        [SerializeField] private ScreenSpaceBoundingBoxDrawer m_screenSpaceBoxDrawer;

        [Tooltip("When false, 3D world-space bounding boxes are not drawn (SentisInferenceUiManager.DrawUIBoxes is skipped).")]
        [SerializeField] private bool m_draw3DBoundingBoxes = true;

        [Header("Logging")]
        [SerializeField] private SharedLogger m_logger;
        [SerializeField] private bool showLog = false;

        [Header("Passthrough Camera")]
        [SerializeField] private PassthroughCameraAccess m_cameraAccess;

        private bool m_isWaiting = false;
        private bool m_hasConnectedToServer = false;
        private bool m_hasStartedDetectionFlow = false;
        private bool m_generateNextFrame = false;
        private bool m_detectionFrozen = false;

        [Range(0f, 1f)]
        public float confidenceThreshold = 0.5f;  // Default: show detections with 50% confidence or higher


        void Start()
        {
            if (m_uiInference != null && m_labelsAsset != null)
            {
                m_uiInference.SetLabels(m_labelsAsset);
            }
        }

        public void RunInference(Texture targetTexture)
        {
            // No longer needed for server-based inference
            // Left blank to maintain compatibility
        }

        public bool IsRunning()
        {
            return true; // Always return true for remote/server inference
        }

        /// <summary>
        /// Request that the next frame sent to the server includes a
        /// 'generate=true' query flag so the backend can save crops/masks.
        /// Safe to call multiple times; it only affects the next request.
        /// </summary>
        public void RequestGenerateForNextFrame()
        {
            m_generateNextFrame = true;
            AppendLog("[Client] Generate requested for next frame.");
        }

        /// <summary>
        /// Freeze detection: stop sending frames and keep current detections and world-space boxes.
        /// </summary>
        public void FreezeDetection()
        {
            m_detectionFrozen = true;
            if (m_uiInference != null)
                m_uiInference.SetAnnotationsFrozen(true);
            AppendLog("[Client] Detection frozen — keeping current boxes and results.");
        }

        /// <summary>
        /// Unfreeze detection: resume sending frames and updating detections/boxes.
        /// </summary>
        public void UnfreezeDetection()
        {
            m_detectionFrozen = false;
            if (m_uiInference != null)
                m_uiInference.SetAnnotationsFrozen(false);
            AppendLog("[Client] Detection unfrozen — resuming updates.");
        }

        private void AppendLog(string message, bool isError = false)
        {

            if (!showLog)
            {
                return;
            }

            if (m_logger != null)
            {
                if (isError)
                {
                    m_logger.LogError(message);
                }
                else
                {
                    m_logger.Log(message);
                }
            }
            else
            {
                if (isError)
                {
                    Debug.LogError(message);
                }
                else
                {
                    Debug.Log(message);
                }
            }
        }

        public void StartDetection()
        {
            if (m_hasConnectedToServer)
            {
                AppendLog("[Client] Already connected to server, starting detection.");
                StartCoroutine(SendFramesToServerRoutine());
                return;
            }

            AppendLog("[Client] Checking server connectivity before starting detection.");
            StartCoroutine(ConnectThenStartRoutine());
        }

        void Update()
        {
            // Automatically start detection once the UI menu is unpaused
            if (!m_hasStartedDetectionFlow && m_uiMenuManager != null && !m_uiMenuManager.IsPaused)
            {
                m_hasStartedDetectionFlow = true;
                AppendLog("[Client] DetectionUiMenuManager unpaused — starting detection.");
                StartDetection();
            }

            // Keep bounding box highlight in sync with proxy label selection (single or multi-select from twist)
            if (m_uiInference != null && m_proxyCreator != null)
            {
                m_proxyCreator.GetSelectionRange(out int minIndex, out int maxIndex);
                Color selectedColor = m_proxyCreator.GetSelectedColor();
                m_uiInference.UpdateSelectionRangeHighlight(minIndex, maxIndex, selectedColor);
            }
        }

        IEnumerator SendFramesToServerRoutine()
        {
            AppendLog("[Client] Coroutine started");

            // Wait for passthrough camera to become available
            while (m_cameraAccess == null || !m_cameraAccess.IsPlaying)
            {
                AppendLog("[Client] Waiting for passthrough camera...");
                yield return new WaitForSeconds(0.2f);
            }

            AppendLog("[Client] Passthrough camera is ready");

            while (true)
            {
                if (m_detectionFrozen)
                {
                    yield return null;
                    continue;
                }
                if (!m_isWaiting)
                {
                    AppendLog("[Client] About to send frame to server");
                    yield return new WaitForEndOfFrame();
                    yield return StartCoroutine(SendFrameToServer());
                }
                else
                {
                    yield return null;
                }
            }
        }

        IEnumerator ConnectThenStartRoutine()
        {
            AppendLog("[Client] Pinging inference server at: " + healthUrl);

            using (UnityWebRequest request = UnityWebRequest.Get(healthUrl))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    m_hasConnectedToServer = true;
                    AppendLog("[Client] Server connectivity check succeeded: " + request.downloadHandler.text);
                    StartCoroutine(SendFramesToServerRoutine());
                }
                else
                {
                    AppendLog("[Client] Failed to reach inference server during connectivity check.", true);
                    AppendLog("[Client] HTTP Error: " + request.error, true);
                    AppendLog("[Client] Status Code: " + request.responseCode, true);
                }
            }
        }

        IEnumerator SendFrameToServer()
        {
            m_isWaiting = true;
            AppendLog("[Client] Capturing frame");

            Texture2D frame = CaptureFrame();
            if (frame == null)
            {
                AppendLog("[Client] CaptureFrame returned null, skipping this frame.", true);
                m_isWaiting = false;
                yield break;
            }

            AppendLog("[Client] Frame captured");
            
            byte[] imageBytes = frame.EncodeToJPG();
            AppendLog("[Client] Frame encoded: " + imageBytes.Length + " bytes");

            Destroy(frame);

            // Append optional generate=true flag for this frame
            string url = serverUrl;
            if (m_generateNextFrame)
            {
                m_generateNextFrame = false; // consume the flag
                url = serverUrl + (serverUrl.Contains("?") ? "&" : "?") + "generate=true";
                AppendLog("[Client] Using generate=true for this request.");
            }

            UnityWebRequest request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(imageBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/octet-stream");

            AppendLog("[Client] Sending POST to: " + serverUrl);
            yield return request.SendWebRequest();

            try
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (!m_hasConnectedToServer)
                    {
                        m_hasConnectedToServer = true;
                        AppendLog("[Client] Successfully connected to inference server.");
                    }

                    string json = request.downloadHandler.text;
                    AppendLog("[Client] Detections received: " + json, false);
                    // Let ProxyInject inspect any optional "analysis" payload (analysis data for labels).
                    if (m_proxyInject != null)
                        m_proxyInject.ProcessServerResponse(json);

                    // When frozen, do not run detection again: keep current boxes and avoid re-invoking OnObjectsDetected.
                    if (!m_detectionFrozen)
                        ProcessDetections(json);
                }
                else
                {
                    AppendLog("[Client] Failed to reach inference server.", true);
                    AppendLog("[Client] HTTP Error: " + request.error, true);
                    AppendLog("[Client] Status Code: " + request.responseCode, true);
                }

                m_isWaiting = false;
            }
            catch (Exception ex)
            {
                AppendLog("[Client] Exception in SendFrameToServer: " + ex.Message, true);
            }
            
        }

        Texture2D CaptureFrame()
        {
            if (m_cameraAccess == null || !m_cameraAccess.IsPlaying)
            {
                AppendLog("[Client] Passthrough camera is not ready.", true);
                return null;
            }

            Texture sourceTex = m_cameraAccess.GetTexture();
            if (sourceTex == null)
            {
                AppendLog("[Client] Passthrough camera texture is null.", true);
                return null;
            }

            RenderTexture rt = new RenderTexture(sourceTex.width, sourceTex.height, 0);
            Graphics.Blit(sourceTex, rt);
            RenderTexture.active = rt;

            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();

            RenderTexture.active = null;
            rt.Release();

            return tex;
        }

        [Serializable]
        public class Detection
        {
            public int x, y, w, h;
            public float confidence;
            public int @class;
        }

        [Serializable]
        private class ServerDetectionsWrapper
        {
            public Detection[] detections;
        }

        void ProcessDetections(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                AppendLog("[Client] Empty detections JSON, skipping.", true);
                return;
            }

            Detection[] detections;

            // Support both legacy "[{...}]" arrays and new "{ detections: [...] }" wrapper.
            string trimmed = json.TrimStart();
            if (trimmed.Length > 0 && trimmed[0] == '[')
            {
                detections = JsonHelper.FromJson<Detection>(json);
            }
            else
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<ServerDetectionsWrapper>(json);
                    detections = wrapper?.detections ?? Array.Empty<Detection>();
                }
                catch (Exception ex)
                {
                    AppendLog("[Client] Failed to parse detections JSON: " + ex.Message, true);
                    return;
                }
            }

            if (m_cameraAccess == null || !m_cameraAccess.IsPlaying)
            {
                AppendLog("[Client] m_cameraAccess is not ready while processing detections.", true);
                return;
            }

            var filtered = new List<Detection>();
            foreach (var det in detections)
            {
                if (det.confidence >= confidenceThreshold)
                {
                    filtered.Add(det);
                }
            }

            AppendLog($"[Client] Filtered detections count: {filtered.Count}");

            // Convert filtered detections into the format expected by SentisInferenceUiManager.DrawUIBoxes:
            // List<(int classId, Vector4 boundingBox)> where boundingBox = (x1, y1, x2, y2) in pixels.
            var uiDetections = new List<(int classId, Vector4 boundingBox)>(filtered.Count);
            foreach (var det in filtered)
            {
                float x1 = det.x;
                float y1 = det.y;
                float x2 = det.x + det.w;
                float y2 = det.y + det.h;
                uiDetections.Add((det.@class, new Vector4(x1, y1, x2, y2)));
            }

            // Use the passthrough camera resolution as the "input size" for normalization,
            // and the current camera pose for 3D placement (matching SentisInferenceRunManager).
            Vector2 inputSize = m_cameraAccess.CurrentResolution;
            Pose cameraPose = m_cameraAccess.GetCameraPose();

            int selectedLabelIndex = -1;
            Color? selectedColor = null;
            Color? normalColor = null;
            if (m_proxyCreator != null)
            {
                selectedLabelIndex = m_proxyCreator.GetSelectedLabelIndex();
                selectedColor = m_proxyCreator.GetSelectedColor();
                normalColor = m_proxyCreator.GetNormalColor();
            }

            if (m_draw3DBoundingBoxes && m_uiInference != null)
            {
                m_uiInference.DrawUIBoxes(uiDetections, inputSize, cameraPose, selectedLabelIndex, selectedColor);
            }

            if (m_screenSpaceBoxDrawer != null)
            {
                m_screenSpaceBoxDrawer.DrawBoxes(uiDetections, inputSize);
            }

            // Sync proxy labels to detection count (driven by 2D boxes on passthrough canvas, not 3D world boxes)
            if (m_proxyCreator != null)
            {
                m_proxyCreator.SyncLabelsWithDetections(uiDetections.Count);
            }

            // Update detection UI menu with current object count (so "Detecting objects: N" is correct when only 2D boxes are drawn)
            if (m_uiMenuManager != null)
            {
                m_uiMenuManager.OnObjectsDetected(uiDetections.Count);
            }

            // if (m_miniCameraOverlay != null)
            // {
            //     showLog = true;
            //     AppendLog("[Client] Drawing boxes on mini camera overlay", true);
            //     m_miniCameraOverlay.DrawBoxes(uiDetections, inputSize);
            //     showLog = false;
            // }
        }
    }

    // Helper class to handle array JSON (Unity's JsonUtility limitation)
    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            string wrappedJson = "{\"Items\":" + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrappedJson);
            return wrapper.Items;
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] Items;
        }
    }
}
