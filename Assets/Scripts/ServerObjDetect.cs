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
        [SerializeField] private TextMeshPro debugLogText;
        [SerializeField] private bool showDebugLog = false;
        [Header("Passthrough Camera")]
        [SerializeField] private PassthroughCameraAccess m_cameraAccess;

        private bool m_isWaiting = false;
        private bool m_hasConnectedToServer = false;

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

        private void AppendLog(string message, bool isError = false)
        {
            if (isError)
            {
                Debug.LogError(message);
            }
            else
            {
                Debug.Log(message);
            }

            // Always log to the Unity console, but only update on-headset debug text
            // when showDebugLog is enabled and a target TextMeshPro is assigned.
            if (!showDebugLog || debugLogText == null)
            {
                return;
            }

            // var line = $"{DateTime.Now:HH:mm:ss} {message}";
            var line = $"{message}";
            debugLogText.text = string.IsNullOrEmpty(debugLogText.text) ? line : $"{debugLogText.text}\n{line}";
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
            // VR controller input (A button)
            if (OVRInput.GetDown(OVRInput.Button.One))
            {
                AppendLog("[Scene] A button pressed — starting detection.");
                StartDetection();
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

            UnityWebRequest request = new UnityWebRequest(serverUrl, "POST");
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

                    // string json = request.downloadHandler.text;
                    AppendLog("[Client] Detections received: " + request.downloadHandler.text);
                    ProcessDetections(request.downloadHandler.text);
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

        void ProcessDetections(string json)
        {
            Detection[] detections = JsonHelper.FromJson<Detection>(json);

            if (m_uiInference == null)
            {
                AppendLog("[Client] m_uiInference is null. Did you forget to assign it?", true);
                return;
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

            m_uiInference.DrawUIBoxes(uiDetections, inputSize, cameraPose);
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
