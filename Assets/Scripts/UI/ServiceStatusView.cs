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

        [Tooltip("Queue summary above the entrance.")]
        [SerializeField] private bool ShowQueueLabel;

        [SerializeField] private Vector3 BuildingLabelOffset = new(0f, 2.2f, 0f);

        [SerializeField] private RectTransform OccupancyBadge;

        [SerializeField] private TMPro.TMP_Text OccupancyText;

        [SerializeField] private Vector3 OccupancyOffset;

        [SerializeField] private RectTransform PatienceBadge;

        [SerializeField] private TMPro.TMP_Text PatienceTime;

        [SerializeField] private UnityEngine.UI.Image PatienceFill;

        [SerializeField] private Vector3 PatienceOffset = new(0f, 0.82f, 0f);


        private WaveController _wave;

        private RectTransform _labelParent;

        private Canvas _labelCanvas;

        private int _planned = -1;

        private int _completed = -1;
        private (int Aboard, int Ashore, int Walking, int AtBuilding) _locations;
        private (int Occupied, int Total) _occupancy = (-1, -1);

        private int _waiting = -1;

        private string _warning;

        private int _oldestSeconds = -1;

        private int _headSeconds = -1;

        private UnityEngine.UI.Button _hudBadge;

        private TMPro.TMP_Text _hudText;

        private string _hudTitle;

        private Vector3 _hudOffset;

        private bool _hudMode;

        private (int Level, int Waiting, int Occupied, int Capacity) _shownHud;


        internal RectTransform LabelParent => BuildingLabel != null ? BuildingLabel.parent as RectTransform : null;

        internal ServiceBuildingBehaviour HudBuilding => Building;


        internal void ConfigureHudBadge(UnityEngine.UI.Button badge, TMPro.TMP_Text text, string title, Vector3 offset)
        {
            _hudBadge = badge;
            _hudText = text;
            _hudTitle = title;
            _hudOffset = offset;
            var colors = badge.colors;
            colors.disabledColor = colors.normalColor;
            badge.colors = colors;
            badge.gameObject.SetActive(false);
            if (BuildingLabel != null && BuildingLabel.TryGetComponent(out UnityEngine.UI.Image summary))
            {
                StyleQueueLabel(summary, BuildingText, badge.image);
                BuildingLabel.SetAsLastSibling();
            }
            if (PatienceBadge != null && PatienceBadge.TryGetComponent(out UnityEngine.UI.Image patience))
            {
                StyleQueueLabel(patience, PatienceTime, badge.image);
                PatienceBadge.SetAsLastSibling();
            }
        }


        internal void SetHudMode(bool enabled)
        {
            _hudMode = enabled && _hudBadge != null;
            _waiting = -1;
            if (_hudMode && OccupancyBadge != null)
                OccupancyBadge.gameObject.SetActive(false);
            if (!_hudMode && _hudBadge != null)
                _hudBadge.gameObject.SetActive(false);
        }


        private void Start()
        {
            if (Waves == null || Waves.Controller == null || Requests == null
                || Requests.Arrivals == null || Requests.Queues == null || Destination == null
                || RatingCatalog == null || !RatingCatalog.TryValidate(out _)
                || Building == null || ResortCamera == null
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

            if (PatienceBadge != null && (PatienceTime == null || PatienceFill == null || PatienceBadge.parent != _labelParent))
            {
                DisableWithError("Assign the patience timer and fill under the building label's Canvas parent.");

                return;
            }

            _wave = Waves.Controller;
        }


        private void LateUpdate()
        {
            if (_wave == null)
                return;

            bool currentQueue = Requests.Queues.WaveNumber == _wave.CurrentWaveNumber;
            if (!_hudMode && CountsText != null)
            {
                ServiceType service = Destination.Service;
                int planned = Requests.Arrivals.WaveNumber == _wave.CurrentWaveNumber
                    ? Requests.Arrivals.GetPlannedCount(service)
                    : Destination.IsAvailable ? _wave.CurrentWave.GetRequestCount(service) : 0;
                int completed = currentQueue ? Requests.Queues.GetCompletedCount(service) : 0;
                var locations = Customers != null ? Customers.GetCustomerLocations(service) : default;
                if (planned != _planned || completed != _completed || locations != _locations)
                {
                    CountsText.text = Customers == null
                        ? $"Požadované: {planned}\nVybavené: {completed}"
                        : $"Plán: {planned} · Hotovo: {completed}\nLoď spolu: {locations.Aboard} · Bez cieľa: {locations.Ashore}\nCesta: {locations.Walking} · Budova: {locations.AtBuilding}";
                    _planned = planned;
                    _completed = completed;
                    _locations = locations;
                }
            }

            bool activeQueue = currentQueue && Destination.IsAvailable && Building.gameObject.activeInHierarchy
                && (_wave.State is WaveState.WaveActive or WaveState.WavePaused);
            int waiting = activeQueue ? Requests.Queues.GetWaitingCount(Destination) : 0;
            if (_hudMode)
                UpdateHudBadge(waiting);
            else
                UpdateOccupancy();

            int oldestSeconds = GetLongWaitSeconds(waiting);
            string warning = string.Empty;
            if (oldestSeconds >= 0)
                warning = "Dlhé čakanie";
            else if (waiting > 0 && !Building.HasWorker)
                warning = "Chýba pracovník";

            if (waiting != _waiting || warning != _warning || oldestSeconds != _oldestSeconds)
            {
                string detail = oldestSeconds >= 0 ? $"{warning} · {FormatTime(oldestSeconds)}" : warning;
                BuildingText.text = _hudMode ? detail : $"Rad: {waiting}";
                if (!_hudMode && detail.Length > 0)
                    BuildingText.text += $"\n{detail}";
                _waiting = waiting;
                _warning = warning;
                _oldestSeconds = oldestSeconds;
            }

            bool showHead = UpdatePatienceBadge(activeQueue && waiting > 0, out Vector3 headAnchor);
            Vector3 summaryAnchor = showHead ? headAnchor : Destination.transform.position + BuildingLabelOffset;
            if (_hudMode && !showHead && waiting > 0
                && Requests.Queues.TryGetWaitingHead(Destination, out Transform waitingHead, out _))
                summaryAnchor = waitingHead.position + PatienceOffset;

            float summaryLift = showHead ? (PatienceBadge.rect.height + BuildingLabel.rect.height) * 0.5f + 6f : 0f;
            UpdateLabelPosition(BuildingLabel, ShowQueueLabel && waiting > 0 && (!_hudMode || warning.Length > 0), summaryAnchor, summaryLift);
            if (_hudMode && _hudBadge.gameObject.activeSelf)
            {
                RaiseBadgeOver(PatienceBadge);
                RaiseBadgeOver(BuildingLabel);
            }
        }


        private static void StyleQueueLabel(UnityEngine.UI.Image background, TMPro.TMP_Text text, UnityEngine.UI.Image style)
        {
            background.sprite = style.sprite;
            background.type = style.type;
            background.pixelsPerUnitMultiplier = style.pixelsPerUnitMultiplier;
            background.color = new Color32(255, 233, 183, 255);
            background.raycastTarget = false;
            if (text != null)
                text.color = new Color32(38, 30, 24, 255);
        }


        private void RaiseBadgeOver(RectTransform queueLabel)
        {
            if (queueLabel == null || !queueLabel.gameObject.activeSelf)
                return;

            RectTransform badge = (RectTransform)_hudBadge.transform;
            Rect queueBounds = BoundsInParent(queueLabel);
            Rect badgeBounds = BoundsInParent(badge);
            if (badgeBounds.Overlaps(queueBounds))
                badge.anchoredPosition += Vector2.up * (queueBounds.yMax - badgeBounds.yMin + 6f);
        }


        private static Rect BoundsInParent(RectTransform label)
        {
            Vector2 size = Vector2.Scale(label.rect.size, (Vector2)label.localScale);
            return new Rect((Vector2)label.localPosition - Vector2.Scale(size, label.pivot), size);
        }


        private void OnDisable()
        {
            if (BuildingLabel != null)
                BuildingLabel.gameObject.SetActive(false);
            if (OccupancyBadge != null)
                OccupancyBadge.gameObject.SetActive(false);
            if (PatienceBadge != null)
                PatienceBadge.gameObject.SetActive(false);
            if (_hudBadge != null)
                _hudBadge.gameObject.SetActive(false);
        }


        private void UpdateHudBadge(int waiting)
        {
            ServiceBuilding service = Building.Building;
            bool show = service != null && Destination.IsAvailable;
            if (show)
            {
                var state = (Level: service.CurrentLevel, Waiting: waiting,
                    Occupied: service.TotalSeats - service.FreeSeats, Capacity: service.TotalSeats);
                if (state != _shownHud)
                {
                    _hudText.text = $"{_hudTitle} · Úroveň {state.Level}\nČaká {state.Waiting}  ·  Obsadené {state.Occupied}/{state.Capacity}";
                    _shownHud = state;
                }
            }

            bool canOpen = _wave.State == WaveState.AwaitingWave && !_wave.IsSequenceComplete;
            _hudBadge.interactable = canOpen;
            _hudBadge.targetGraphic.raycastTarget = canOpen;
            UpdateLabelPosition((RectTransform)_hudBadge.transform, show, Destination.transform.position + _hudOffset);
        }


        private void UpdateOccupancy()
        {
            if (OccupancyBadge == null || OccupancyText == null)
                return;

            ServiceBuilding service = Building.Building;
            bool show = service != null && Destination.IsAvailable;
            if (show)
            {
                var occupancy = (Occupied: service.TotalSeats - service.FreeSeats, Total: service.TotalSeats);
                if (occupancy != _occupancy)
                {
                    OccupancyText.text = $"{occupancy.Occupied}/{occupancy.Total}";
                    _occupancy = occupancy;
                }
            }

            UpdateLabelPosition(OccupancyBadge, show, Destination.transform.position + OccupancyOffset);
        }


        private int GetLongWaitSeconds(int waiting)
        {
            if (waiting == 0 || !Requests.Queues.TryPeekWaiting(Destination, out QueuedRequest oldest))
                return -1;

            double elapsed = _wave.ElapsedSeconds - oldest.WaitingSinceSeconds;
            return elapsed > RatingCatalog.PatienceGrace ? (int)elapsed : -1;
        }


        private bool UpdatePatienceBadge(bool show, out Vector3 anchor)
        {
            anchor = default;
            if (PatienceBadge == null)
                return false;

            if (show && Requests.Queues.TryGetWaitingHead(Destination, out Transform guest, out double waitingSince))
            {
                double elapsed = _wave.ElapsedSeconds - waitingSince;
                if (elapsed > RatingCatalog.PatienceGrace)
                {
                    anchor = guest.position + PatienceOffset;
                    int seconds = (int)elapsed;
                    if (seconds != _headSeconds)
                    {
                        PatienceTime.text = FormatTime(seconds);
                        _headSeconds = seconds;
                    }

                    PatienceFill.fillAmount = 1f - Mathf.InverseLerp(RatingCatalog.PatienceGrace, RatingCatalog.PatienceLimit, (float)elapsed);
                    UpdateLabelPosition(PatienceBadge, true, anchor);
                    return PatienceBadge.gameObject.activeSelf;
                }
            }

            if (PatienceBadge.gameObject.activeSelf)
                PatienceBadge.gameObject.SetActive(false);

            return false;
        }


        internal static string FormatTime(int seconds)
        {
            return $"{seconds / 60}:{seconds % 60:00}";
        }


        private void UpdateLabelPosition(RectTransform label, bool show, Vector3 worldPosition, float lift = 0f)
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

            position.y += lift;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_labelParent, ResortCamera.pixelRect.min, uiCamera, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_labelParent, ResortCamera.pixelRect.max, uiCamera, out Vector2 max);
            Vector2 size = Vector2.Scale(label.rect.size, (Vector2)label.localScale);
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
