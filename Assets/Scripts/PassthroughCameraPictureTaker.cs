// Copyright (c) Meta Platforms, Inc.
// Quentin request - PassthroughCameraPictureTaker

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PassthroughCameraSamples
{
    public class PassthroughCameraPictureTaker : MonoBehaviour
    {
        [Header("Camera input")]
        [SerializeField] private WebCamTextureManager webCamManager;

        [Header("UI - live view under a Mask")]
        [SerializeField] private RectTransform mask;      // This RawImage sits under a Mask
        [SerializeField] private RawImage mainImage;      // This RawImage sits under a Mask
        [SerializeField] private RawImage previewImage;      // This RawImage sits under a Mask
        [SerializeField] private Slider zoomSlider;       // 0 to 1
        [SerializeField] private float maxZoomScale = 2f; // 1 means no zoom

        [Header("Display crop")]
        [Tooltip("Centered portion of the live camera frame to display. 0.8 means the center 80% of the frame.")]
        [Range(0.1f, 1f)] [SerializeField] private float centerCropPercent = 0.8f;

        [Header("Flash and preview")]
        [SerializeField] private Image flashlight;        // White Image on top of everything
        [SerializeField] private float flashInSeconds = 0.06f;
        [SerializeField] private float flashOutSeconds = 0.15f;
        [SerializeField] private float previewSeconds = 2f;

        [Header("Fallback")]
        [SerializeField] private Texture2D debugImage;    // Used when webcam is not available

        // Captured pictures you can use to build a gallery
        public IReadOnlyList<Texture2D> CapturedPictures => _capturedPictures;
        private readonly List<Texture2D> _capturedPictures = new List<Texture2D>();

        // Internals
        private Texture _liveTexture;
        private Color32[] _pixelBuffer;   // full-frame reuse buffer
        private Color32[] _cropBuffer;    // crop reuse buffer
        private Coroutine _previewRoutine;

        private void Awake()
        {
            if (flashlight != null)
            {
                var c = flashlight.color;
                c.a = 0f;
                flashlight.color = c;
            }
        }

        private void OnEnable()
        {
            previewImage.texture = null;
            ApplyLiveViewCrop();
            StartCoroutine(BindLiveTextureWhenReady());
        }

        private IEnumerator BindLiveTextureWhenReady()
        {
            float timeout = 3f;
            float t = 0f;

            while (webCamManager != null &&
                   webCamManager.WebCamTexture == null &&
                   t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            _liveTexture = (webCamManager != null && webCamManager.WebCamTexture != null)
                ? webCamManager.WebCamTexture
                : debugImage;

            if (mainImage != null)
            {
                mainImage.texture = _liveTexture;
                ApplyLiveViewCrop();
            }
        }

        private void Update()
        {
            if (mainImage == null)
                return;

            float zoomValue = zoomSlider != null ? zoomSlider.value : 0f;
            float scale = Mathf.Lerp(1f, maxZoomScale, zoomValue);
            mainImage.transform.localScale = new Vector3(scale, scale, 1f);
            ApplyLiveViewCrop();
        }

        /// <summary>
        /// Capture the current camera frame using a center crop based on the current zoom scale, then preview it.
        /// </summary>
        public void TakePicture()
        {
            Texture2D result;

            // Current zoom scale used by the preview
            float scale = Mathf.Lerp(1f, maxZoomScale, zoomSlider.value);

            WebCamTexture wct = webCamManager != null ? webCamManager.WebCamTexture : null;

            if (wct != null && wct.width > 16 && wct.height > 16)
            {
                result = CaptureMaskedFromWebCamAligned(mainImage, mask, webCamManager.WebCamTexture);
            }
            else
            {
                result = debugImage;
            }

            _capturedPictures.Add(result);

            previewImage.texture = result;
            previewImage.enabled = true;

            if (_previewRoutine != null) StopCoroutine(_previewRoutine);
            _previewRoutine = StartCoroutine(FlashAndPreview(result));
        }

        private Texture2D CaptureMaskedFromWebCamAligned(RawImage img, RectTransform mask, WebCamTexture wct)
        {
            if (img == null || mask == null || wct == null) return null;
            int srcW = wct.width;
            int srcH = wct.height;
            if (srcW <= 16 || srcH <= 16) return null;

            // 1) Get rects
            Rect imgRect = img.rectTransform.rect;
            Rect maskRect = mask.rect;

            // 2) Express mask in RawImage local space (since aligned, only scale matters)
            Vector3 s = img.rectTransform.localScale;
            Vector2 maskSizeInImageLocal = new Vector2(maskRect.width / Mathf.Abs(s.x),
                                                       maskRect.height / Mathf.Abs(s.y));
            Vector2 imgCenter = (imgRect.min + imgRect.max) * 0.5f;
            Rect maskInImageLocal = new Rect(imgCenter - maskSizeInImageLocal * 0.5f, maskSizeInImageLocal);

            // 3) Intersection in RawImage local space
            Rect inter = Intersect(imgRect, maskInImageLocal);
            if (inter.width <= 0 || inter.height <= 0) return null;

            // 4) Normalized intersection [0..1] in RawImage rect
            float u0n = (inter.xMin - imgRect.xMin) / imgRect.width;
            float u1n = (inter.xMax - imgRect.xMin) / imgRect.width;
            float v0n = (inter.yMin - imgRect.yMin) / imgRect.height;
            float v1n = (inter.yMax - imgRect.yMin) / imgRect.height;

            // 5) Map through uvRect
            Rect uvr = img.uvRect;
            float u0 = Mathf.Lerp(uvr.xMin, uvr.xMax, u0n);
            float u1 = Mathf.Lerp(uvr.xMin, uvr.xMax, u1n);
            float v0 = Mathf.Lerp(uvr.yMin, uvr.yMax, v0n);
            float v1 = Mathf.Lerp(uvr.yMin, uvr.yMax, v1n);

            // 6) Convert UVs to pixel rectangle
            int px0 = Mathf.RoundToInt(Mathf.Min(u0, u1) * srcW);
            int px1 = Mathf.RoundToInt(Mathf.Max(u0, u1) * srcW);
            int py0 = Mathf.RoundToInt(Mathf.Min(v0, v1) * srcH);
            int py1 = Mathf.RoundToInt(Mathf.Max(v0, v1) * srcH);

            int w = Mathf.Max(1, px1 - px0);
            int h = Mathf.Max(1, py1 - py0);

            // 7) Grab pixels
            Color32[] fullFrame = wct.GetPixels32();
            Color32[] cropped = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                int srcIndex = (py0 + y) * srcW + px0;
                int dstIndex = y * w;
                System.Array.Copy(fullFrame, srcIndex, cropped, dstIndex, w);
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(cropped);
            tex.Apply();
            return tex;
        }

        private static Rect Intersect(Rect a, Rect b)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin) return new Rect(0, 0, 0, 0);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>
        /// Visible camera window in normalized source UVs after applying the configured center crop and runtime zoom.
        /// Marker overlays can use this so they line up with the cropped preview.
        /// </summary>
        public Rect GetVisibleNormalizedUvRect()
        {
            Rect visibleUv = mainImage != null ? mainImage.uvRect : GetCenteredCropUvRect();

            if (mainImage == null)
                return visibleUv;

            float scaleX = Mathf.Max(0.0001f, Mathf.Abs(mainImage.rectTransform.localScale.x));
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(mainImage.rectTransform.localScale.y));

            float visibleWidth = Mathf.Clamp01(visibleUv.width / scaleX);
            float visibleHeight = Mathf.Clamp01(visibleUv.height / scaleY);

            float x = visibleUv.x + (visibleUv.width - visibleWidth) * 0.5f;
            float y = visibleUv.y + (visibleUv.height - visibleHeight) * 0.5f;
            return new Rect(x, y, visibleWidth, visibleHeight);
        }

        private void ApplyLiveViewCrop()
        {
            if (mainImage == null)
                return;

            mainImage.uvRect = GetCenteredCropUvRect();
        }

        private Rect GetCenteredCropUvRect()
        {
            float cropPercent = Mathf.Clamp(centerCropPercent, 0.1f, 1f);
            float margin = (1f - cropPercent) * 0.5f;
            return new Rect(margin, margin, cropPercent, cropPercent);
        }

        private Texture2D CaptureCroppedFromWebCam(WebCamTexture wct, float scale)
        {
            int srcW = wct.width;
            int srcH = wct.height;

            // Crop size decreases as scale increases
            int cropW = Mathf.RoundToInt(srcW / scale);
            int cropH = Mathf.RoundToInt(srcH / scale);

            int x0 = (srcW - cropW) / 2;
            int y0 = (srcH - cropH) / 2;

            // Fill full-frame buffer
            if (_pixelBuffer == null || _pixelBuffer.Length != srcW * srcH)
                _pixelBuffer = new Color32[srcW * srcH];
            wct.GetPixels32(_pixelBuffer);

            // Prepare crop buffer
            if (_cropBuffer == null || _cropBuffer.Length != cropW * cropH)
                _cropBuffer = new Color32[cropW * cropH];

            // Copy centered crop
            for (int y = 0; y < cropH; y++)
            {
                int srcIndex = (y0 + y) * srcW + x0;
                int dstIndex = y * cropW;
                System.Array.Copy(_pixelBuffer, srcIndex, _cropBuffer, dstIndex, cropW);
            }

            var tex = new Texture2D(cropW, cropH, TextureFormat.RGBA32, false);
            tex.SetPixels32(_cropBuffer);
            tex.Apply(false, false);
            return tex;
        }

        private IEnumerator FlashAndPreview(Texture2D photo)
        {
            yield return FlashCoroutine();

            if (previewImage != null && photo != null)
            {
                previewImage.texture = photo;
                previewImage.enabled = true;
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, previewSeconds));
                previewImage.texture = null;
                previewImage.enabled = false;
            }
        }

        private IEnumerator FlashCoroutine()
        {
            if (flashlight == null) yield break;

            float t = 0f;
            while (t < flashInSeconds)
            {
                t += Time.unscaledDeltaTime;
                SetFlashAlpha(Mathf.Lerp(0f, 1f, flashInSeconds <= 0f ? 1f : t / flashInSeconds));
                yield return null;
            }
            SetFlashAlpha(1f);

            t = 0f;
            while (t < flashOutSeconds)
            {
                t += Time.unscaledDeltaTime;
                SetFlashAlpha(Mathf.Lerp(1f, 0f, flashOutSeconds <= 0f ? 1f : t / flashOutSeconds));
                yield return null;
            }
            SetFlashAlpha(0f);
        }

        private void SetFlashAlpha(float a)
        {
            if (flashlight == null) return;
            var c = flashlight.color;
            c.a = a;
            flashlight.color = c;
        }

        public void DisposeAllCaptured()
        {
            foreach (var tex in _capturedPictures)
            {
                if (tex != null) Destroy(tex);
            }
            _capturedPictures.Clear();
        }
    }
}
