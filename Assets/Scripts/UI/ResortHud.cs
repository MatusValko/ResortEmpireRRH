using System;
using System.Collections.Generic;

using ResortEmpire.Buildings;
using ResortEmpire.Economy;
using ResortEmpire.Requests;
using ResortEmpire.Services;
using ResortEmpire.Waves;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    // Built at runtime from the game's existing Canvas and destinations.
    internal sealed class ResortHud
    {
        private static readonly Color Ink = new Color32(38, 30, 24, 255);
        private static readonly Color Surface = new Color32(253, 251, 245, 255);
        private static readonly Color Panel = new Color32(214, 236, 240, 255);
        private static readonly Color Muted = new Color32(86, 78, 68, 255);
        private static readonly Color Green = new Color32(20, 119, 87, 255);
        private static readonly Color Amber = new Color32(255, 216, 130, 255);
        private static readonly Color Track = new Color32(217, 225, 217, 255);

        private sealed class Row
        {
            public ServiceBuildingBehaviour Building;
            public string Name;
            public RectTransform Card;
            public TMP_Text Count;
            public TMP_Text Status;
            public RectTransform Progress;
            public Button Worker;
            public TMP_Text WorkerText;
            public Button Boost;
            public TMP_Text BoostText;
            public Button Donor;
        }

        private readonly LandscapeLayout _layout;
        private readonly ResortWindows _windows;
        private readonly WaveController _wave;
        private readonly RequestArrivalsBehaviour _requests;
        private readonly WorkersManagers _workers;
        private readonly EconomyBehaviour _economy;
        private readonly EnergyBoostsBehaviour _boosts;
        private readonly TMP_FontAsset _font;
        private readonly Sprite _pill;
        private readonly List<Row> _rows = new();
        private readonly List<ServiceStatusView> _mapViews = new();
        private readonly List<(CanvasGroup Group, float Alpha, bool BlocksRaycasts)> _legacy = new();
        private readonly RectTransform _wavePanel;
        private readonly RectTransform _sheet;
        private readonly TMP_Text _waveTitle;
        private readonly TMP_Text _waveTimer;
        private readonly TMP_Text _pauseText;
        private readonly TMP_Text _sheetTitle;
        private readonly TMP_Text _workerStatus;
        private readonly Button _chooseWorker;
        private readonly TMP_Text _chooseWorkerText;
        private readonly TMP_Text _upgradeDetails;
        private readonly Button _upgrade;
        private readonly TMP_Text _upgradeText;
        private readonly Transform _start;
        private readonly RectTransform _arrivalProgress;

        private Row _selected;
        private bool _enabled;
        private bool _waveVisible;
        private float _slide;
        private float _refreshAt;


        internal ResortHud(LandscapeLayout layout, ResortWindows windows, WaveController wave,
            RequestArrivalsBehaviour requests, WorkersManagers workers, EconomyBehaviour economy,
            EnergyBoostsBehaviour boosts, TMP_FontAsset fallbackFont)
        {
            _layout = layout;
            _windows = windows;
            _wave = wave;
            _requests = requests;
            _workers = workers;
            _economy = economy;
            _boosts = boosts;

            RectTransform area = layout.HudArea;
            Transform coins = area.Find("TopBar/CoinsCounter");
            _pill = coins != null ? coins.GetComponent<Image>()?.sprite : null;
            _font = (coins != null ? coins.GetComponentInChildren<TMP_Text>()?.font : null) ?? fallbackFont;
            _start = area.Find("StartWaveButton");
            AddLegacyGroup(area.Find("ServiceStatisticsScroll"));
            AddLegacyGroup(area.Find("WorkerControls"));
            AddLegacyGroup(area.Find("OverloadAlert"));

            _wavePanel = Box(area, "Active Wave HUD", Panel, true);
            _wavePanel.anchorMin = new Vector2(1f, 0f);
            _wavePanel.anchorMax = Vector2.one;
            _wavePanel.pivot = new Vector2(1f, 0.5f);
            const float scale = LandscapeLayout.WavePanelScale;
            _waveTitle = Text(_wavePanel, "Wave", string.Empty, 30, 20, TextAlignmentOptions.MidlineLeft);
            Top(_waveTitle.rectTransform, 14f * scale, 0.58f, 0f, 8f * scale, 46f * scale);
            Button pause = Action(_wavePanel, "Pause", "Pauza", Surface, TogglePause, out _pauseText);
            Top((RectTransform)pause.transform, 0f, 1f, 0.59f, 10f * scale, 42f * scale, 10f * scale);
            _waveTimer = Text(_wavePanel, "Arrival Timer", string.Empty, 24, 17, TextAlignmentOptions.MidlineLeft);
            Top(_waveTimer.rectTransform, 14f * scale, 1f, 0f, 56f * scale, 32f * scale, 12f * scale);
            RectTransform arrivalTrack = Box(_wavePanel, "Arrival Track", Track, false, false);
            Top(arrivalTrack, 14f * scale, 1f, 0f, 92f * scale, 7f * scale, 14f * scale);
            _arrivalProgress = Progress(arrivalTrack);

            RectTransform list = Rect(_wavePanel, "Building Cards");
            Place(list, 0f, 0f, 1f, 1f);
            list.offsetMin = new Vector2(10f, 10f) * scale;
            list.offsetMax = new Vector2(-10f, -111f) * scale;
            ListLayout(list, 7f * scale);

            _sheet = Box(area, "Building Upgrade HUD", Panel, true);
            Place(_sheet, 0.13f, 0f, 0.84f, 0f);
            _sheet.offsetMin = new Vector2(0f, 12f);
            _sheet.offsetMax = new Vector2(0f, 328f);
            _sheetTitle = Text(_sheet, "Building Heading", string.Empty, 33, 22, TextAlignmentOptions.MidlineLeft);
            Place(_sheetTitle.rectTransform, 0.025f, 0.82f, 0.90f, 0.97f);
            Button close = Action(_sheet, "Close", "Zavrieť", Surface, CloseBuilding, out _);
            Place((RectTransform)close.transform, 0.89f, 0.83f, 0.98f, 0.96f);

            RectTransform staff = Box(_sheet, "Worker Section", Surface);
            Place(staff, 0.02f, 0.07f, 0.49f, 0.80f);
            _workerStatus = Text(staff, "Worker Status", string.Empty, 25, 17, TextAlignmentOptions.MidlineLeft);
            Place(_workerStatus.rectTransform, 0.04f, 0.78f, 0.96f, 0.98f);
            _chooseWorker = Action(staff, "Choose Worker", string.Empty, Surface, ChooseSheetWorker, out _chooseWorkerText);
            Place((RectTransform)_chooseWorker.transform, 0.04f, 0.51f, 0.96f, 0.74f);
            RectTransform donors = Rect(staff, "Available Workers");
            Place(donors, 0.04f, 0.06f, 0.96f, 0.75f);
            ListLayout(donors, 5f);

            RectTransform upgrades = Box(_sheet, "Upgrade Section", Surface);
            Place(upgrades, 0.51f, 0.07f, 0.98f, 0.80f);
            _upgradeDetails = Text(upgrades, "Upgrade Details", string.Empty, 27, 21, TextAlignmentOptions.MidlineLeft);
            Place(_upgradeDetails.rectTransform, 0.05f, 0.38f, 0.95f, 0.93f);
            _upgrade = Action(upgrades, "Upgrade", string.Empty, Amber, UpgradeBuilding, out _upgradeText);
            Place((RectTransform)_upgrade.transform, 0.05f, 0.09f, 0.95f, 0.34f);

            var destinations = (ServiceDestination[])layout.HudDestinations.Clone();
            Array.Sort(destinations, (left, right) => left.Service.CompareTo(right.Service));
            foreach (ServiceDestination destination in destinations)
            {
                ServiceBuildingBehaviour building = destination.GetComponent<ServiceBuildingBehaviour>();
                if (building != null && building.Building != null)
                    AddBuilding(list, donors, building);
            }

            foreach (TMP_Text label in _wavePanel.GetComponentsInChildren<TMP_Text>())
            {
                label.fontSizeMin *= scale;
                label.fontSizeMax *= scale;
            }

            foreach (ServiceStatusView view in UnityEngine.Object.FindObjectsByType<ServiceStatusView>(FindObjectsSortMode.None))
                InstallMapBadge(view);

            _wavePanel.gameObject.SetActive(false);
            _sheet.gameObject.SetActive(false);
        }


        internal void SetEnabled(bool enabled)
        {
            if (_enabled == enabled)
                return;

            _enabled = enabled;
            _workers.OnBuildingTapped = enabled ? TapBuilding : null;
            foreach (var legacy in _legacy)
            {
                if (legacy.Group == null) continue;
                legacy.Group.alpha = enabled ? 0f : legacy.Alpha;
                legacy.Group.blocksRaycasts = !enabled && legacy.BlocksRaycasts;
            }
            foreach (ServiceStatusView view in _mapViews)
            {
                if (view != null)
                    view.SetHudMode(enabled);
            }

            if (enabled)
            {
                _layout.SetWavePanelProgress(_slide);
                return;
            }

            if (_sheet != null) CloseBuilding();
            if (_wavePanel != null) _wavePanel.gameObject.SetActive(false);
            if (_start != null) _start.gameObject.SetActive(true);
            if (_layout != null && _layout.HudArea != null) _layout.RestoreLegacyFraming();
        }


        internal void Refresh()
        {
            if (!_enabled)
                return;

            if (_wave.State != WaveState.WavePaused)
                _waveVisible = _wave.State == WaveState.WaveActive;
            if (_selected != null && !_windows.CanOpenPreviews)
                CloseBuilding();
            if (_start != null)
                _start.gameObject.SetActive(_wave.State == WaveState.AwaitingWave && !_wave.IsSequenceComplete);

            _slide = Mathf.MoveTowards(_slide, _waveVisible ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            float reveal = Mathf.SmoothStep(0f, 1f, _slide);
            float width = _layout.WavePanelWidth;
            _wavePanel.sizeDelta = new Vector2(width, -112f);
            _wavePanel.anchoredPosition = new Vector2((1f - reveal) * width, -40f);
            _wavePanel.gameObject.SetActive(_waveVisible || reveal > 0f);
            _layout.SetWavePanelProgress(reveal);

            if (Time.unscaledTime < _refreshAt)
                return;

            _refreshAt = Time.unscaledTime + 0.1f;
            if (_waveVisible) RefreshWavePanel();
            if (_selected != null) RefreshSheet();
        }


        internal void OpenBuilding(ServiceBuilding building)
        {
            foreach (Row row in _rows)
            {
                if (row.Building.Building == building)
                {
                    TapBuilding(row.Building);
                    return;
                }
            }
        }


        internal void CloseBuilding()
        {
            _selected = null;
            _sheet.gameObject.SetActive(false);
        }


        private bool TapBuilding(ServiceBuildingBehaviour building)
        {
            if (!_enabled || !_windows.CanOpenPreviews || !building.Destination.IsAvailable)
                return false;

            _selected = _rows.Find(row => row.Building == building);
            if (_selected == null)
                return false;

            _sheet.gameObject.SetActive(true);
            RefreshSheet();
            return true;
        }


        private void RefreshWavePanel()
        {
            _waveTitle.text = $"Vlna {_wave.CurrentWaveNumber}/{WaveCatalog.RequiredWaveCount}";
            _pauseText.text = _wave.State == WaveState.WavePaused ? "Pokračovať" : "Pauza";
            float duration = _wave.CurrentWave.ArrivalDurationSeconds;
            float remaining = Mathf.Max(0f, duration - (float)_wave.ElapsedSeconds);
            _waveTimer.text = remaining > 0f ? $"Príchod · {ServiceStatusView.FormatTime(Mathf.CeilToInt(remaining))}" : "Obsluha hostí";
            SetProgress(_arrivalProgress, 1f - remaining / duration);

            foreach (Row row in _rows)
            {
                ServiceBuildingBehaviour building = row.Building;
                ServiceDestination destination = building.Destination;
                bool available = destination.IsAvailable;
                row.Card.gameObject.SetActive(available);
                if (!available) continue;

                int completed = _requests.Queues.GetCompletedCount(destination.Service);
                int planned = _requests.Arrivals.GetPlannedCount(destination.Service);
                int waiting = _requests.Queues.GetWaitingCount(destination);
                row.Count.text = $"{completed}/{planned}";
                row.Status.text = building.HasWorker ? $"Čaká {waiting} · Pracuje" : $"Čaká {waiting} · Bez pracovníka";
                row.Status.color = waiting > 0 && !building.HasWorker ? new Color32(141, 48, 30, 255) : Muted;
                SetProgress(row.Progress, planned > 0 ? (float)completed / planned : 0f);
                RefreshWorkerButton(building, row.Worker, row.WorkerText);
                RefreshBoost(row);
            }
        }


        private void RefreshSheet()
        {
            ServiceBuildingBehaviour target = _selected.Building;
            ServiceBuilding building = target.Building;
            _sheetTitle.text = $"{_selected.Name} · Úroveň {building.CurrentLevel} · Miesta {building.TotalSeats}";
            _workerStatus.text = target.HasWorker ? "Pracovník pridelený" : "Voľné miesto pre pracovníka";
            RefreshWorkerButton(target, _chooseWorker, _chooseWorkerText);
            _chooseWorker.gameObject.SetActive(target.HasWorker || _workers.chosenWorker != null);
            foreach (Row row in _rows)
            {
                bool visible = _workers.chosenWorker == null && row.Building != target
                    && row.Building.HasWorker && row.Building.assignedWorker != null && _workers.CanAssignWorker(target);
                row.Donor.gameObject.SetActive(visible);
            }

            var balance = _economy.Wallet.Balance;
            var missing = Math.Max(0L, building.NextUpgradeCost - balance);
            if (building.IsMaxLevel)
            {
                _upgradeDetails.text = $"Úroveň {building.CurrentLevel}\nNajvyššia úroveň";
                _upgrade.gameObject.SetActive(false);
                return;
            }

            _upgradeDetails.text = $"Úroveň {building.CurrentLevel} → {building.CurrentLevel + 1}\nMiesta {building.TotalSeats} → {building.NextLevelSeats}";
            _upgradeText.text = missing > 0 ? $"Chýba {missing} mincí" : $"Vylepšiť · {building.NextUpgradeCost} mincí";
            _upgrade.gameObject.SetActive(true);
            _upgrade.interactable = missing == 0;
        }


        private void AddBuilding(RectTransform list, RectTransform donors, ServiceBuildingBehaviour building)
        {
            string name = DisplayName(building.Destination.Service);
            var row = new Row { Building = building, Name = name, Card = Box(list, name + " Card", Surface) };
            LayoutElement height = row.Card.gameObject.AddComponent<LayoutElement>();
            height.minHeight = 0f;
            height.preferredHeight = 130f * LandscapeLayout.WavePanelScale;
            TMP_Text title = Text(row.Card, "Service", name, 27, 18, TextAlignmentOptions.MidlineLeft);
            Place(title.rectTransform, 0.06f, 0.72f, 0.72f, 0.97f);
            row.Count = Text(row.Card, "Completed", string.Empty, 24, 16, TextAlignmentOptions.MidlineRight);
            Place(row.Count.rectTransform, 0.73f, 0.72f, 0.94f, 0.97f);
            RectTransform track = Box(row.Card, "Service Track", Track, false, false);
            Place(track, 0.06f, 0.65f, 0.94f, 0.71f);
            row.Progress = Progress(track);
            row.Status = Text(row.Card, "Queue", string.Empty, 22, 16, TextAlignmentOptions.MidlineLeft);
            Place(row.Status.rectTransform, 0.06f, 0.37f, 0.94f, 0.63f);
            row.Worker = Action(row.Card, "Worker", "Presunúť", Panel, () => UseWorkerButton(building), out row.WorkerText);
            Place((RectTransform)row.Worker.transform, 0.05f, 0.06f, 0.49f, 0.34f);
            row.Boost = Action(row.Card, "Energy", string.Empty, Amber, () => UseBoost(building), out row.BoostText);
            Place((RectTransform)row.Boost.transform, 0.51f, 0.06f, 0.95f, 0.34f);
            row.Donor = Action(donors, name, $"Presunúť z {name}", Panel, () => MoveDonor(building), out _);
            LayoutElement donorHeight = row.Donor.gameObject.AddComponent<LayoutElement>();
            donorHeight.minHeight = 25f;
            donorHeight.preferredHeight = 38f;
            _rows.Add(row);
        }


        private void RefreshWorkerButton(ServiceBuildingBehaviour building, Button button, TMP_Text label)
        {
            Worker chosen = _workers.chosenWorker;
            bool selecting = chosen != null;
            bool source = selecting && chosen.assignedBuilding == building;
            bool canMove = selecting ? source || _workers.CanAssignWorker(building) : building.assignedWorker != null;
            button.interactable = _windows.CanMoveWorkers && canMove;
            label.text = source ? "Zrušiť" : selecting ? "Presunúť sem" : "Pracovník";
        }


        private void UseWorkerButton(ServiceBuildingBehaviour building)
        {
            if (!_windows.CanMoveWorkers) return;
            Worker chosen = _workers.chosenWorker;
            if (chosen == null && building.assignedWorker != null)
                _workers.ChooseWorker(building.assignedWorker);
            else if (chosen != null && chosen.assignedBuilding == building)
                _workers.CancelWorkerAssignment();
            else if (chosen != null && _workers.CanAssignWorker(building))
                _workers.TryAssignWorker(building, chosen);
            _refreshAt = 0f;
        }


        private void ChooseSheetWorker()
        {
            if (_selected != null)
                UseWorkerButton(_selected.Building);
        }


        private void MoveDonor(ServiceBuildingBehaviour donor)
        {
            if (_selected != null && _windows.CanOpenPreviews && _workers.CanAssignWorker(_selected.Building))
                _workers.TryAssignWorker(_selected.Building, donor.assignedWorker);
            _refreshAt = 0f;
        }


        private void RefreshBoost(Row row)
        {
            BoostOffer offer = _boosts != null ? _boosts.GetOffer(row.Building) : BoostOffer.None;
            row.Boost.gameObject.SetActive(offer != BoostOffer.None);
            row.Boost.interactable = offer is BoostOffer.Boost or BoostOffer.WatchAd;
            row.BoostText.text = offer switch
            {
                BoostOffer.Boost => $"Zrýchliť · {_boosts.BoostCost}",
                BoostOffer.WatchAd => $"Reklama +{_boosts.AdEnergy}",
                BoostOffer.Running => $"{Mathf.CeilToInt(_boosts.GetRemainingSeconds(row.Building))} s",
                _ => string.Empty
            };
        }


        private void UseBoost(ServiceBuildingBehaviour building)
        {
            if (_boosts == null || _wave.State != WaveState.WaveActive) return;
            switch (_boosts.GetOffer(building))
            {
                case BoostOffer.Boost:
                    _boosts.TryBoost(building);
                    break;
                case BoostOffer.WatchAd:
                    _boosts.TryWatchAd();
                    break;
            }
            _refreshAt = 0f;
        }


        private void UpgradeBuilding()
        {
            if (_selected != null && _windows.CanOpenPreviews)
            {
                _selected.Building.Building.TryUpgrade(_economy.Wallet);
                RefreshSheet();
            }
        }


        private void TogglePause()
        {
            if (_wave.State == WaveState.WavePaused)
                _wave.TryResume();
            else if (_wave.State == WaveState.WaveActive)
                _wave.TryPause();
            _refreshAt = 0f;
        }


        private void InstallMapBadge(ServiceStatusView view)
        {
            ServiceBuildingBehaviour building = view.HudBuilding;
            if (view.LabelParent == null || building == null || building.Destination == null)
                return;

            Button badge = Action(view.LabelParent, building.name + " Status HUD", string.Empty, Surface,
                () => TapBuilding(building), out TMP_Text text);
            RectTransform rect = (RectTransform)badge.transform;
            rect.sizeDelta = new Vector2(302f, 70f);
            rect.localScale = Vector3.one * 1.1f;
            text.fontSizeMin = 16f;
            text.fontSizeMax = 24f;
            if (!building.TryGetModelBounds(out Bounds bounds))
                bounds = new Bounds(building.transform.position, Vector3.zero);

            var world = new Vector3(bounds.center.x, bounds.max.y + 0.5f, bounds.center.z);
            view.ConfigureHudBadge(badge, text, DisplayName(building.Destination.Service), world - building.transform.position);
            _mapViews.Add(view);
        }


        private void AddLegacyGroup(Transform legacy)
        {
            if (legacy == null) return;
            if (!legacy.TryGetComponent(out CanvasGroup group))
                group = legacy.gameObject.AddComponent<CanvasGroup>();
            _legacy.Add((group, group.alpha, group.blocksRaycasts));
        }


        private static string DisplayName(ServiceType service) => service switch
        {
            ServiceType.Rooms => "Recepcia",
            ServiceType.Food => "Reštaurácia",
            ServiceType.Drinks => "Beach Bar",
            ServiceType.Relax => "Bazén",
            ServiceType.OceanView => "Bungalovy",
            _ => service.ToString()
        };


        private RectTransform Box(Transform parent, string name, Color color, bool raycast = false, bool rounded = true)
        {
            RectTransform rect = Rect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            if (rounded) StyleImage(image, color);
            else image.color = color;
            image.raycastTarget = raycast;
            return rect;
        }


        private void StyleImage(Image image, Color color)
        {
            image.sprite = _pill;
            image.type = _pill != null ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 2.2f;
            image.color = color;
        }


        private TMP_Text Text(Transform parent, string name, string value, float maxSize, float minSize, TextAlignmentOptions align)
        {
            var text = Rect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.enableAutoSizing = true;
            text.fontSizeMin = minSize;
            text.fontSizeMax = maxSize;
            text.color = Ink;
            text.alignment = align;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }


        private Button Action(Transform parent, string name, string caption, Color tint, UnityEngine.Events.UnityAction action, out TMP_Text label)
        {
            RectTransform rect = Box(parent, name, tint, true);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(action);
            label = Text(rect, "Label", caption, 24, 14, TextAlignmentOptions.Center);
            Place(label.rectTransform, 0.04f, 0.08f, 0.96f, 0.92f);
            return button;
        }


        private static RectTransform Rect(Transform parent, string name)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }


        private static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }


        private static void Top(RectTransform rect, float leftInset, float rightAnchor, float leftAnchor,
            float offset, float height, float rightInset = 0f)
        {
            Place(rect, leftAnchor, 1f, rightAnchor, 1f);
            rect.offsetMin = new Vector2(leftInset, -offset - height);
            rect.offsetMax = new Vector2(-rightInset, -offset);
        }


        private static void ListLayout(RectTransform rect, float gap)
        {
            VerticalLayoutGroup group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = gap;
            group.childControlHeight = true;
            group.childControlWidth = true;
            group.childForceExpandHeight = false;
            group.childForceExpandWidth = true;
        }


        private RectTransform Progress(RectTransform track)
        {
            RectTransform fill = Box(track, "Fill", Green, false, false);
            Place(fill, 0f, 0f, 0f, 1f);
            return fill;
        }


        private static void SetProgress(RectTransform bar, float fraction)
        {
            bar.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }
    }
}
