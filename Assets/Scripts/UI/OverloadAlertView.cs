using System;

using ResortEmpire.Buildings;
using ResortEmpire.Economy;
using UnityEngine;

namespace ResortEmpire.UI
{
    // Warning card for an overloaded building: move a worker there, spend energy on a boost, or ignore it
    [DisallowMultipleComponent]
    public sealed class OverloadAlertView : MonoBehaviour
    {
        [Serializable]
        private sealed class AlertBuilding
        {
            public ServiceBuildingBehaviour Building;

            [Tooltip("Name in the warning, in the form that follows \"v\": v reštaurácii, v hoteli…")]
            public string NameInLocative;
        }


        [SerializeField] private EnergyBoostsBehaviour Boosts;

        [SerializeField] private WorkersManagers Workers;

        [SerializeField] private ResortWindows Windows;

        [SerializeField] private AlertBuilding[] Buildings = Array.Empty<AlertBuilding>();

        [SerializeField] private GameObject Card;

        [SerializeField] private TMPro.TMP_Text Title;

        [SerializeField] private TMPro.TMP_Text Message;

        [SerializeField] private UnityEngine.UI.Button MoveWorkerButton;

        [SerializeField] private UnityEngine.UI.Button BoostButton;

        [SerializeField] private TMPro.TMP_Text BoostLabel;

        [SerializeField] private UnityEngine.UI.Button IgnoreButton;


        private OverloadAlerts _alerts;

        private bool[] _overloaded;

        private bool[] _canHelp;

        private int _shown = -1;

        private bool _shownCanMove;

        private bool _shownCanBoost;


        private void Start()
        {
            if (Boosts == null || Workers == null || Windows == null || Buildings == null || Card == null || Title == null
                || Message == null || MoveWorkerButton == null || BoostButton == null || BoostLabel == null || IgnoreButton == null
                || Array.Exists(Buildings, entry => entry == null || entry.Building == null))
            {
                Debug.LogError($"{nameof(OverloadAlertView)} on '{name}': Assign systems, buildings and all card references.", this);
                enabled = false;

                return;
            }

            _alerts = new OverloadAlerts(Buildings.Length);
            _overloaded = new bool[Buildings.Length];
            _canHelp = new bool[Buildings.Length];
            Subscribe();
        }


        private void OnEnable()
        {
            if (_alerts != null)
                Subscribe();
        }


        private void OnDisable()
        {
            if (MoveWorkerButton != null)
                MoveWorkerButton.onClick.RemoveListener(OnMoveWorker);

            if (BoostButton != null)
                BoostButton.onClick.RemoveListener(OnBoost);

            if (IgnoreButton != null)
                IgnoreButton.onClick.RemoveListener(OnIgnore);

            if (Card != null)
                Card.SetActive(false);

            _shown = -1;
        }


        private void LateUpdate()
        {
            for (int i = 0; i < Buildings.Length; i++)
            {
                ServiceBuildingBehaviour building = Buildings[i].Building;
                _overloaded[i] = Boosts.IsOverloaded(building);
                _canHelp[i] = CanMoveWorkerTo(building) || Boosts.GetOffer(building) == BoostOffer.Boost;
            }

            int current = _alerts.Refresh(_overloaded, _canHelp);
            Show(Windows.CanMoveWorkers ? current : -1);
        }


        private void Subscribe()
        {
            MoveWorkerButton.interactable = true;
            BoostButton.interactable = true;
            IgnoreButton.interactable = true;
            MoveWorkerButton.onClick.RemoveListener(OnMoveWorker);
            MoveWorkerButton.onClick.AddListener(OnMoveWorker);
            BoostButton.onClick.RemoveListener(OnBoost);
            BoostButton.onClick.AddListener(OnBoost);
            IgnoreButton.onClick.RemoveListener(OnIgnore);
            IgnoreButton.onClick.AddListener(OnIgnore);
        }


        private void Show(int index)
        {
            bool visible = index >= 0;
            if (Card.activeSelf != visible)
                Card.SetActive(visible);

            if (!visible)
            {
                _shown = -1;

                return;
            }

            ServiceBuildingBehaviour building = Buildings[index].Building;
            bool canMove = CanMoveWorkerTo(building);
            bool canBoost = Boosts.GetOffer(building) == BoostOffer.Boost;
            if (index == _shown && canMove == _shownCanMove && canBoost == _shownCanBoost)
                return;

            _shown = index;
            _shownCanMove = canMove;
            _shownCanBoost = canBoost;
            Title.SetText($"Nával {Buildings[index].NameInLocative}!");
            Message.SetText(building.HasWorker
                ? "Hostia prichádzajú rýchlejšie, než ich budova stíha obslúžiť."
                : "Hostia čakajú v rade a nikto tam nepracuje.");
            MoveWorkerButton.gameObject.SetActive(canMove);
            BoostButton.gameObject.SetActive(canBoost);
            BoostLabel.SetText($"Zrýchliť · {Boosts.BoostCost}");
        }


        // In the prototype every building has one worker slot, so the move takes the worker
        // from the staffed building with the shortest queue
        private bool CanMoveWorkerTo(ServiceBuildingBehaviour target)
        {
            return !target.HasWorker && target.assignedWorker == null && FindWorkerToMove(target) != null;
        }


        private Worker FindWorkerToMove(ServiceBuildingBehaviour target)
        {
            Worker best = null;
            int bestWaiting = int.MaxValue;
            foreach (AlertBuilding entry in Buildings)
            {
                ServiceBuildingBehaviour source = entry.Building;
                if (source == target || !source.HasWorker || source.assignedWorker == null || !source.assignedWorker.isActiveAndEnabled)
                    continue;

                int waiting = Boosts.GetWaitingRequests(source);
                if (waiting < bestWaiting)
                {
                    best = source.assignedWorker;
                    bestWaiting = waiting;
                }
            }

            return best;
        }


        private void OnMoveWorker()
        {
            if (_alerts.Current < 0)
                return;

            ServiceBuildingBehaviour target = Buildings[_alerts.Current].Building;
            Worker worker = FindWorkerToMove(target);
            if (worker != null && CanMoveWorkerTo(target))
                Workers.TryAssignWorker(target, worker);

            _alerts.DismissCurrent();
        }


        private void OnBoost()
        {
            if (_alerts.Current < 0)
                return;

            Boosts.TryBoost(Buildings[_alerts.Current].Building);
            _alerts.DismissCurrent();
        }


        private void OnIgnore()
        {
            _alerts.DismissCurrent();
        }
    }
}
