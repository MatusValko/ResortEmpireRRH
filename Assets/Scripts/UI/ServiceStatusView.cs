using ResortEmpire.Requests;
using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.UI
{
    [DisallowMultipleComponent]
    public sealed class ServiceStatusView : MonoBehaviour
    {
        [SerializeField] private WaveControllerBehaviour Waves;

        [SerializeField] private RequestArrivalsBehaviour Requests;

        [SerializeField] private ServiceDestination Destination;

        [SerializeField] private ServiceBuilding Building;

        [SerializeField] private Camera ResortCamera;

        [SerializeField] private TMPro.TMP_Text CountsText;

        [SerializeField] private RectTransform BuildingLabel;

        [SerializeField] private TMPro.TMP_Text BuildingText;

        [SerializeField] private Vector3 BuildingLabelOffset = new(0f, 2.2f, 0f);


        private WaveController _wave;

        private RectTransform _labelParent;

        private Canvas _labelCanvas;

        private int _planned = -1;

        private int _completed = -1;

        private int _waiting = -1;

        private bool _hasWorker;


        private void Start()
        {
            if (Waves == null || Waves.Controller == null || Requests == null
                || Requests.Arrivals == null || Requests.Queues == null || Destination == null
                || Building == null || ResortCamera == null || CountsText == null
                || BuildingLabel == null || BuildingText == null)
            {
                DisableWithError("Assign initialized Waves and Requests, building references, camera and UI labels.");

                return;
            }

            _labelParent = BuildingLabel.parent as RectTransform;
            _labelCanvas = BuildingText.canvas;
            if (_labelParent == null || _labelCanvas == null)
            {
                DisableWithError("Building label must be inside a Canvas with a RectTransform parent.");

                return;
            }

            _wave = Waves.Controller;
        }


        private void LateUpdate()
        {
            if (_wave == null)
                return;

            ServiceType service = Destination.Service;
            int planned = Requests.Arrivals.WaveNumber == _wave.CurrentWaveNumber
                ? Requests.Arrivals.GetPlannedCount(service)
                : Destination.IsAvailable ? _wave.CurrentWave.GetRequestCount(service) : 0;
            bool currentQueue = Requests.Queues.WaveNumber == _wave.CurrentWaveNumber;
            int completed = currentQueue ? Requests.Queues.GetCompletedCount(service) : 0;
            int waiting = currentQueue ? Requests.Queues.GetWaitingCount(service) : 0;
            bool hasWorker = Building.HasWorker;

            if (planned != _planned || completed != _completed)
            {
                CountsText.text = $"Požadované: {planned}\nVybavené: {completed}";
                _planned = planned;
                _completed = completed;
            }

            if (waiting != _waiting || hasWorker != _hasWorker)
            {
                // shortcut: Capacity is a UI example, replace when Buildings exposes capacity.
                BuildingText.text = $"Miesta: 3 (ukážka)\nRad: {waiting}  ·  Pracovník: {(hasWorker ? "áno" : "nie")}";
                _waiting = waiting;
                _hasWorker = hasWorker;
            }

            UpdateLabelPosition();
        }


        private void OnDisable()
        {
            if (BuildingLabel != null)
                BuildingLabel.gameObject.SetActive(false);
        }


        private void UpdateLabelPosition()
        {
            Vector3 screenPosition = ResortCamera.WorldToScreenPoint(Destination.transform.position + BuildingLabelOffset);
            bool visible = Building.gameObject.activeInHierarchy && screenPosition.z > 0f
                && ResortCamera.pixelRect.Contains(new Vector2(screenPosition.x, screenPosition.y));
            if (BuildingLabel.gameObject.activeSelf != visible)
                BuildingLabel.gameObject.SetActive(visible);

            if (!visible)
                return;

            Camera uiCamera = _labelCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _labelCanvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_labelParent, screenPosition, uiCamera, out Vector2 position))
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_labelParent, ResortCamera.pixelRect.min, uiCamera, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_labelParent, ResortCamera.pixelRect.max, uiCamera, out Vector2 max);
            Vector2 size = BuildingLabel.rect.size;
            Vector2 pivot = BuildingLabel.pivot;
            position.x = size.x <= max.x - min.x
                ? Mathf.Clamp(position.x, min.x + size.x * pivot.x, max.x - size.x * (1f - pivot.x))
                : (min.x + max.x) * 0.5f + size.x * (pivot.x - 0.5f);
            position.y = size.y <= max.y - min.y
                ? Mathf.Clamp(position.y, min.y + size.y * pivot.y, max.y - size.y * (1f - pivot.y))
                : (min.y + max.y) * 0.5f + size.y * (pivot.y - 0.5f);
            BuildingLabel.anchoredPosition = position;
        }


        private void DisableWithError(string message)
        {
            Debug.LogError($"{nameof(ServiceStatusView)} on '{name}': {message}", this);
            if (CountsText != null)
                CountsText.text = "Údaje nie sú dostupné";

            enabled = false;
        }
    }
}
