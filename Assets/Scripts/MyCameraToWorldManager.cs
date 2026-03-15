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

        [SerializeField] private Vector3 m_headSpaceDebugShift = new(0, -.15f, .4f);

        private bool m_isDebugOn;
        private bool m_snapshotTaken;
        private OVRPose m_snapshotHeadPose;

        private void OnEnable() => OVRManager.display.RecenteredPose += RecenterCallBack;

        private void OnDisable() => OVRManager.display.RecenteredPose -= RecenterCallBack;

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
        }

        private void Update()
        {
            if (InputManager.IsButtonADownOrPinchStarted())
            {
                m_snapshotTaken = !m_snapshotTaken;
                if (m_snapshotTaken)
                {
                    m_cameraCanvas.MakeCameraSnapshot();
                    m_snapshotHeadPose = m_centerEyeAnchor.transform.ToOVRPose();
                    UpdateMarkerPoses();
                    m_cameraAccess.enabled = false;
                }
                else
                {
                    m_cameraAccess.enabled = true;
                    m_cameraCanvas.ResumeStreamingFromCamera();
                }
            }

            if (InputManager.IsButtonBDownOrMiddleFingerPinchStarted())
            {
                m_isDebugOn = !m_isDebugOn;
                Debug.Log($"PCA: SpatialSnapshotManager: DEBUG mode is {(m_isDebugOn ? "ON" : "OFF")}");
            }

            if (!m_snapshotTaken)
            {
                UpdateMarkerPoses();
            }
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

            if (m_isDebugOn)
            {
                var direction = m_snapshotTaken ? m_snapshotHeadPose.orientation : m_centerEyeAnchor.transform.rotation;
                m_cameraCanvas.transform.position += direction * m_headSpaceDebugShift;
            }
        }

        private void RecenterCallBack()
        {
            if (m_snapshotTaken)
            {
                m_snapshotTaken = false;
                m_cameraAccess.enabled = true;
                m_cameraCanvas.ResumeStreamingFromCamera();
            }
        }
    }
