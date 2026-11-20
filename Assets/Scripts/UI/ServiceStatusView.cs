using ResortEmpire.Buildings;
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

        [SerializeField] private WaveRatingCatalog RatingCatalog;

        [SerializeField] private ServiceDestination Destination;

        [SerializeField] private ServiceBuildingBehaviour Building;

        [SerializeField] private Camera ResortCamera;

        [SerializeField] private TMPro.TMP_Text CountsText;

        [SerializeField] private CustomerArrivalBehaviour Customers;

        [SerializeField] private RectTransform BuildingLabel;

        [SerializeField] private TMPro.TMP_Text BuildingText;

        [SerializeField] private Vector3 BuildingLabelOffset = new(0f, 2.2f, 0f);

        [SerializeField] private RectTransform OccupancyBadge;

        [SerializeField] private TMPro.TMP_Text OccupancyText;

        [SerializeField] private Vector3 OccupancyOffset;


        private WaveController _wave;

        private RectTransform _labelParent;

        private Canvas _labelCanvas;

        private int _planned = -1;

        private int _completed = -1;
        private (int Aboard, int Ashore, int Walking, int AtBuilding) _locations;
        private (int Occupied, int Total) _occupancy = (-1, -1);

        private int _waiting = -1;

        private string _warning;


        private void Start()
        {
            if (Waves == null || Waves.Controller == null || Requests == null
                || Requests.Arrivals == null || Requests.Queues == null || Destination == null
                || RatingCatalog == null || !RatingCatalog.TryValidate(out _)
                || Building == null || ResortCamera == null || CountsText == null
                || BuildingLabel == null || BuildingText == null)
            {
                DisableWithError("Assign initialized Waves and Requests, a valid rating catalog, building references, camera and UI labels.");

                return;
            }

            _labelParent = BuildingLabel.parent as RectTransform;
            _labelCanvas = BuildingLabel.GetComponentInParent<Canvas>(true);
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
            var locations = Customers != null ? Customers.GetCustomerLocations(service) : default;
            if (planned != _planned || completed != _completed || locations != _locations)
            {
                CountsText.text = Customers == null
                    ? $"Požadované: {planned}\nVybavené: {completed}"
                    : $"Plán: {planned} · Hotovo: {completed}\nLoď: {locations.Aboard} · Breh: {locations.Ashore}\nCesta: {locations.Walking} · Budova: {locations.AtBuilding}";
                _planned = planned;
                _completed = completed;
                _locations = locations;
            }

            UpdateOccupancy();

            int waiting = currentQueue ? Requests.Queues.GetWaitingCount(service) : 0;
            string warning = GetWarning(currentQueue, Building.HasWorker, service);
            if (waiting != _waiting || warning != _warning)
            {
                BuildingText.text = $"Rad: {waiting}";
                if (warning.Length > 0)
                    BuildingText.text += $"\n{warning}";
                _waiting = waiting;
                _warning = warning;
            }

            UpdateLabelPosition(BuildingLabel, _wave.State == WaveState.WaveActive && waiting > 0 && Building.gameObject.activeInHierarchy,
                Destination.transform.position + BuildingLabelOffset);
        }


        private void OnDisable()
        {
            if (BuildingLabel != null)
                BuildingLabel.gameObject.SetActive(false);
            if (OccupancyBadge != null)
                OccupancyBadge.gameObject.SetActive(false);
        }


        private void UpdateOccupancy()
        {
            if (OccupancyBadge == null || OccupancyText == null)
                return;

            ServiceBuilding service = Building.Building;
            bool show = service != null && Destination.IsAvailable;
            if (show)
            {
                var occupancy = (service.InServiceCount, service.TotalSeats);
                if (occupancy != _occupancy)
                {
                    OccupancyText.text = $"{occupancy.InServiceCount}/{occupancy.TotalSeats}";
                    _occupancy = occupancy;
                }
            }

            UpdateLabelPosition(OccupancyBadge, show, Destination.transform.position + OccupancyOffset);
        }


        private string GetWarning(bool currentQueue, bool hasWorker, ServiceType service)
        {
            if (_wave.State != WaveState.WaveActive || !currentQueue
                || !Requests.Queues.TryPeekWaiting(service, out QueuedRequest oldest))
                return string.Empty;

            if (!hasWorker)
                return "Chýba pracovník";

            return _wave.ElapsedSeconds - oldest.WaitingSinceSeconds > RatingCatalog.PatienceGrace
                ? "Dlhé čakanie"
                : string.Empty;
        }


        private void UpdateLabelPosition(RectTransform label, bool show, Vector3 worldPosition)
        {
            Vector3 screenPosition = ResortCamera.WorldToScreenPoint(worldPosition);
            bool visible = show && screenPosition.z > 0f
                && ResortCamera.pixelRect.Contains(new Vector2(screenPosition.x, screenPosition.y));
            if (label.gameObject.activeSelf != visible)
                label.gameObject.SetActive(visible);

            if (!visible)
                return;

            Camera uiCamera = _labelCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _labelCanvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_labelParent, screenPosition, uiCamera, out Vector2 position))
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_labelParent, ResortCamera.pixelRect.min, uiCamera, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_labelParent, ResortCamera.pixelRect.max, uiCamera, out Vector2 max);
            Vector2 size = label.rect.size;
            Vector2 pivot = label.pivot;
            position.x = size.x <= max.x - min.x
                ? Mathf.Clamp(position.x, min.x + size.x * pivot.x, max.x - size.x * (1f - pivot.x))
                : (min.x + max.x) * 0.5f + size.x * (pivot.x - 0.5f);
            position.y = size.y <= max.y - min.y
                ? Mathf.Clamp(position.y, min.y + size.y * pivot.y, max.y - size.y * (1f - pivot.y))
                : (min.y + max.y) * 0.5f + size.y * (pivot.y - 0.5f);
            label.anchoredPosition = position;
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
