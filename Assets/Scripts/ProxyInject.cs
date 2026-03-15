using System;
using UnityEngine;
using TMPro;

/// <summary>
/// Decodes extended server responses that may contain an "analysis" section.
/// If the JSON has only detections, this component does nothing.
/// Attach it somewhere in the scene and assign it to ServerObjDetector.
/// </summary>
public class ProxyInject : MonoBehaviour
{
    [Header("Label binding")]
    [SerializeField]
    private RectTransform m_labelsParent;

    [Header("Logging")]
    [SerializeField]
    private SharedLogger m_logger;

    [Serializable]
    public class Attributes
    {
        public string size;
        public string state;
        public string position;
        public string notes;
    }

    [Serializable]
    public class AnalysisNode
    {
        public string name;
        public string type;
        public string material;
        public string color;
        public Attributes attributes;
        public AnalysisNode[] children;
    }

    [Serializable]
    public class AnalysisItem
    {
        public string image;

        // Backend sends: "response": "[{...}, {...}]"
        // JSON array encoded as a string.
        public string response;
    }

    [Serializable]
    public class DetectionDTO
    {
        public int x, y, w, h;
        public float confidence;
        public int @class;
    }

    [Serializable]
    public class ServerResponse
    {
        public DetectionDTO[] detections;
        public AnalysisItem[] analysis;
    }

    public ServerResponse LastResponse { get; private set; }

    private void AppendLog(string message, bool isError = false)
    {
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

    void Start()
    {
        AppendLog("[ProxyInject] Ready.");
    }

    [Serializable]
    private class AnalysisArrayWrapper
    {
        public AnalysisNode[] Items;
    }

    /// <summary>
    /// Entry point called from ServerObjDetector with the raw JSON.
    /// If the payload contains an "analysis" section, it is decoded and cached.
    /// Otherwise this method returns without doing anything.
    /// </summary>
    public void ProcessServerResponse(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            AppendLog("[ProxyInject] ProcessServerResponse called with empty JSON.");
            return;
        }

        var trimmed = json.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] == '[' || !trimmed.Contains("\"analysis\""))
        {
            return;
        }

        // AppendLog("[ProxyInject] ProcessServerResponse called with JSON: " + json);

        try
        {
            var parsed = JsonUtility.FromJson<ServerResponse>(json);
            if (parsed == null || parsed.analysis == null || parsed.analysis.Length == 0)
            {
                return;
            }

            LastResponse = parsed;

            // Optional: log all analysis objects for debugging
            for (int ai = 0; ai < parsed.analysis.Length; ai++)
            {
                var item = parsed.analysis[ai];
                if (item == null || string.IsNullOrEmpty(item.response))
                    continue;

                var wrappedJson = "{\"Items\":" + item.response + "}";
                var wrapper = JsonUtility.FromJson<AnalysisArrayWrapper>(wrappedJson);
                var nodes = wrapper?.Items;
                if (nodes == null || nodes.Length == 0)
                    continue;

                AppendLog($"[ProxyInject] Analysis item {ai}, image='{item.image}', objects={nodes.Length}.");

                for (int ni = 0; ni < nodes.Length; ni++)
                {
                    var node = nodes[ni];
                    if (node == null) continue;
                    AppendLog(
                        $"[ProxyInject]  Object {ni}: name='{node.name}', type='{node.type}', color='{node.color}'."
                    );
                }
            }

            UpdateLabelsFromAnalysis(parsed);
        }
        catch (Exception ex)
        {
            AppendLog($"[ProxyInject] Failed to parse analysis JSON: {ex.Message}", true);
        }
    }

    private void UpdateLabelsFromAnalysis(ServerResponse response)
    {
        if (m_labelsParent == null)
        {
            const string warn = "[ProxyInject] Labels parent is not assigned; cannot apply analysis names to labels.";
            AppendLog(warn);
            return;
        }

        if (response.analysis == null || response.analysis.Length == 0)
        {
            return;
        }

        int count = Mathf.Min(response.analysis.Length, m_labelsParent.childCount);
        for (int i = 0; i < count; i++)
        {
            var labelGO = m_labelsParent.GetChild(i).gameObject;
            var analysisItem = response.analysis[i];

            if (analysisItem == null)
            {
                continue;
            }

            // Parse analysisItem.response (JSON array string) into AnalysisNode[]
            string content = analysisItem.response;
            AnalysisNode[] nodes = null;

            if (!string.IsNullOrEmpty(content))
            {
                try
                {
                    var wrappedJson = "{\"Items\":" + content + "}";
                    var wrapper = JsonUtility.FromJson<AnalysisArrayWrapper>(wrappedJson);
                    nodes = wrapper?.Items;
                }
                catch (Exception ex)
                {
                    AppendLog("[ProxyInject] Failed to parse analysis.response content for label update: " + ex.Message, true);
                    continue;
                }
            }

            string displayName = null;
            string nodeType = null;
            if (nodes != null && nodes.Length > 0 && nodes[0] != null)
            {
                displayName = nodes[0].name;
                nodeType = nodes[0].type;
            }

            if (string.IsNullOrEmpty(displayName))
            {
                continue;
            }

            labelGO.name = displayName;

            var text = labelGO.GetComponentInChildren<TextMeshPro>();
            if (text != null)
            {
                text.text = displayName;
            }

            // Inform per-label status component about the analysis data so it
            // can update text appropriately when the label is clicked.
            var status = labelGO.GetComponent<ProxyLabelStatus>();
            if (status != null)
            {
                status.SetAnalysisData(displayName, nodeType);
            }
        }
    }
}


