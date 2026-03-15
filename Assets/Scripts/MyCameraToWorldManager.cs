using System;
using System.Collections;
using Meta.XR;
using Meta.XR.Samples;
using PassthroughCameraSamples.CameraToWorld;
using UnityEngine;
using UnityEngine.Assertions;

public class MyCameraToWorldManager : MonoBehaviour
{
        [SerializeField] private PassthroughCameraAccess m_cameraAccess;
        [SerializeField] private GameObject m_centerEyeAnchor;

        [SerializeField] private CameraToWorldCameraCanvas m_cameraCanvas;
        [SerializeField] private float m_canvasDistance = 1f;

        private IEnumerator Start()
        {
            if (m_cameraAccess == null)
            {
                Debug.LogError($"PCA: {nameof(m_cameraAccess)} field is required "
                            + $"for the component {nameof(MyCameraToWorldManager)} to operate properly");
                enabled = false;
                yield break;
            }

            Assert.IsTrue(m_cameraAccess.enabled, "m_cameraAccess.enabled");
            while (!m_cameraAccess.IsPlaying)
            {
                yield return null;
            }

            ScaleCameraCanvas();
            // Ensure the canvas shows the live passthrough (not a blank texture)
            m_cameraCanvas.ResumeStreamingFromCamera();
        }

        private void Update()
        {
            UpdateMarkerPoses();
        }

        /// <summary>
        /// Calculate the dimensions of the canvas based on the distance from the camera origin and the camera resolution
        /// </summary>
        private void ScaleCameraCanvas()
        {
            var cameraCanvasRectTransform = m_cameraCanvas.GetComponentInChildren<RectTransform>();
            var leftSidePointInCamera = m_cameraAccess.ViewportPointToRay(new Vector2(0f, 0.5f));
            var rightSidePointInCamera = m_cameraAccess.ViewportPointToRay(new Vector2(1f, 0.5f));
            var horizontalFoVDegrees = Vector3.Angle(leftSidePointInCamera.direction, rightSidePointInCamera.direction);
            var horizontalFoVRadians = horizontalFoVDegrees / 180 * Math.PI;
            var newCanvasWidthInMeters = 2 * m_canvasDistance * Math.Tan(horizontalFoVRadians / 2);
            var localScale = (float)(newCanvasWidthInMeters / cameraCanvasRectTransform.sizeDelta.x);
            cameraCanvasRectTransform.localScale = new Vector3(localScale, localScale, localScale);
        }

        /// <summary>
        /// Keep the canvas fixed in front of the camera / center eye so it follows as the user moves.
        /// Uses passthrough camera pose when available; otherwise drives from center eye anchor.
        /// </summary>
        private void UpdateMarkerPoses()
        {
            Vector3 position;
            Quaternion rotation;

            if (m_cameraAccess.IsPlaying)
            {
                var cameraPose = m_cameraAccess.GetCameraPose();
                position = cameraPose.position + cameraPose.rotation * Vector3.forward * m_canvasDistance;
                rotation = cameraPose.rotation;
            }
            else
            {
                // When passthrough isn't playing (e.g. right after resume), follow center eye anchor
                var t = m_centerEyeAnchor.transform;
                position = t.position + t.forward * m_canvasDistance;
                rotation = t.rotation;
            }

            m_cameraCanvas.transform.position = position;
            m_cameraCanvas.transform.rotation = rotation;
        }
    }
