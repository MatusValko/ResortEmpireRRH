using System;

using ResortEmpire.Buildings;
using ResortEmpire.Services;
using UnityEngine;

namespace ResortEmpire.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [DefaultExecutionOrder(-50)]
    public sealed class LandscapeLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform SafeArea;

        [SerializeField] private RectTransform ModalSafeArea;

        [SerializeField] private RectTransform ModalWindow;

        [SerializeField] private RectTransform ResortViewport;

        [SerializeField] private Camera ResortCamera;

        [SerializeField] private ServiceDestination[] Destinations = Array.Empty<ServiceDestination>();


        private readonly Vector3[] _corners = new Vector3[4];

        private Vector2Int _screenSize;

        private Rect _safeArea;

        private Rect _canvasPixels;

        private float _uiScale;


        private void Start()
        {
            if (SafeArea == null || ModalSafeArea == null || ModalWindow == null || ResortViewport == null
                || ResortCamera == null || !ResortCamera.orthographic || Destinations == null
                || Destinations.Length == 0 || Array.Exists(Destinations, destination => destination == null))
            {
                Debug.LogError($"{nameof(LandscapeLayout)} on '{name}': Assign safe areas, window, viewport, orthographic camera and buildings.", this);
                enabled = false;

                return;
            }

            ApplyLayout();
        }


        private void LateUpdate()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (_screenSize.x != Screen.width || _screenSize.y != Screen.height || _safeArea != Screen.safeArea
                || _canvasPixels != canvas.pixelRect || _uiScale != canvas.scaleFactor)
                ApplyLayout();
        }


        private void ApplyLayout()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safeArea = Screen.safeArea;
            Vector2 min = new(Mathf.Clamp01(safeArea.xMin / Screen.width), Mathf.Clamp01(safeArea.yMin / Screen.height));
            Vector2 max = new(Mathf.Clamp01(safeArea.xMax / Screen.width), Mathf.Clamp01(safeArea.yMax / Screen.height));
            Canvas canvas = GetComponent<Canvas>();
            if (max.x <= min.x || max.y <= min.y || canvas.pixelRect.width <= 0f || canvas.pixelRect.height <= 0f)
                return;

            foreach (RectTransform area in new[] { SafeArea, ModalSafeArea })
            {
                area.anchorMin = min;
                area.anchorMax = max;
                area.offsetMin = Vector2.zero;
                area.offsetMax = Vector2.zero;
            }

            Canvas.ForceUpdateCanvases();
            Vector2 safeSize = ModalSafeArea.rect.size;
            ModalWindow.sizeDelta = new Vector2(Mathf.Clamp(safeSize.x - 48f, 0f, 900f), Mathf.Clamp(safeSize.y - 48f, 0f, 720f));
            Vector2 resortSize = SafeArea.rect.size;
            ResortViewport.anchorMin = Vector2.zero;
            ResortViewport.anchorMax = Vector2.one;
            ResortViewport.offsetMin = new Vector2(Mathf.Min(20f, resortSize.x * 0.02f), Mathf.Min(216f, resortSize.y * 0.3f));
            ResortViewport.offsetMax = new Vector2(-Mathf.Min(320f, resortSize.x * 0.35f), -Mathf.Min(128f, resortSize.y * 0.2f));
            ResortViewport.GetWorldCorners(_corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, _corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(null, _corners[2]);
            Rect canvasPixels = canvas.pixelRect;
            Rect viewport = Rect.MinMaxRect(Mathf.Clamp01((bottomLeft.x - canvasPixels.xMin) / canvasPixels.width),
                Mathf.Clamp01((bottomLeft.y - canvasPixels.yMin) / canvasPixels.height),
                Mathf.Clamp01((topRight.x - canvasPixels.xMin) / canvasPixels.width),
                Mathf.Clamp01((topRight.y - canvasPixels.yMin) / canvasPixels.height));
            if (viewport.width <= 0f || viewport.height <= 0f)
                return;

            ResortCamera.rect = new Rect(0f, 0f, 1f, 1f);
            Rect framingPixels = new(canvasPixels.x + viewport.x * canvasPixels.width,
                canvasPixels.y + viewport.y * canvasPixels.height,
                viewport.width * canvasPixels.width, viewport.height * canvasPixels.height);
            FrameBuildings(canvas.scaleFactor, framingPixels);
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            _safeArea = safeArea;
            _canvasPixels = canvasPixels;
            _uiScale = canvas.scaleFactor;
        }


        private void FrameBuildings(float uiScale, Rect framingPixels)
        {
            Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            void IncludeBounds(Bounds bounds)
            {
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 local = ResortCamera.transform.InverseTransformPoint(corner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }

            foreach (ServiceDestination destination in Destinations)
            {
                IncludeBounds(new Bounds(destination.transform.position, Vector3.zero));
                foreach (Renderer renderer in destination.GetComponentsInChildren<Renderer>())
                    IncludeBounds(renderer.bounds);

                ServiceBuildingBehaviour building = destination.GetComponent<ServiceBuildingBehaviour>();
                if (building != null && building.workerPoint != null)
                    IncludeBounds(new Bounds(building.workerPoint.position, Vector3.one));
            }

            Rect pixels = ResortCamera.pixelRect;
            // Reserve room above the models for the building status labels.
            float topPadding = Mathf.Min(96f * uiScale, framingPixels.height * 0.3f);
            float bottomPadding = Mathf.Min(24f * uiScale, framingPixels.height * 0.1f);
            float halfHeight = (max.y - min.y) * 0.5f * pixels.height
                / Mathf.Max(1f, framingPixels.height - topPadding - bottomPadding);
            float halfWidth = (max.x - min.x) * 0.5f * pixels.width
                / Mathf.Max(1f, framingPixels.width - Mathf.Min(80f * uiScale, framingPixels.width * 0.3f));
            float aspect = pixels.width / pixels.height;
            ResortCamera.orthographicSize = Mathf.Max(0.01f, Mathf.Max(halfHeight, halfWidth / aspect));
            Vector3 center = (min + max) * 0.5f;
            Vector2 targetCenter = framingPixels.center + new Vector2(0f, (bottomPadding - topPadding) * 0.5f);
            float horizontalOffset = (targetCenter.x - pixels.center.x) * 2f * ResortCamera.orthographicSize * aspect / pixels.width;
            float verticalOffset = (targetCenter.y - pixels.center.y) * 2f * ResortCamera.orthographicSize / pixels.height;
            ResortCamera.transform.position += ResortCamera.transform.right * (center.x - horizontalOffset)
                + ResortCamera.transform.up * (center.y - verticalOffset);
        }
    }
}
