using ResortEmpire.Buildings;
using ResortEmpire.Services;
using UnityEngine;

namespace ResortEmpire.UI
{
    [DisallowMultipleComponent]
    public sealed class BuildingControls : MonoBehaviour
    {
        [SerializeField] private ResortWindows Windows;

        [SerializeField] private WorkersManagers Workers;

        [SerializeField] private ServiceDestination Destination;

        [SerializeField] private ServiceBuildingBehaviour Building;

        [SerializeField] private UnityEngine.UI.Button MoveButton;

        [SerializeField] private TMPro.TMP_Text MoveButtonText;


        private bool _initialized;


        private void OnEnable()
        {
            if (_initialized)
                Subscribe();
        }


        private void Start()
        {
            if (Windows == null || Workers == null || Destination == null || Building == null
                || MoveButton == null || MoveButtonText == null)
            {
                Debug.LogError($"{nameof(BuildingControls)} on '{name}': Assign building, systems and button references.", this);
                enabled = false;

                return;
            }

            _initialized = true;
            Subscribe();
        }


        private void LateUpdate()
        {
            RefreshButtons();
        }


        private void OnDisable()
        {
            if (MoveButton != null)
            {
                MoveButton.onClick.RemoveListener(OnMoveClicked);
                MoveButton.interactable = false;
            }
        }


        private void Subscribe()
        {
            MoveButton.onClick.AddListener(OnMoveClicked);
            RefreshButtons();
        }


        private void RefreshButtons()
        {
            Worker chosen = Workers.chosenWorker;
            bool selectingTarget = chosen != null;
            MoveButton.interactable = Windows.CanMoveWorkers && Workers.isActiveAndEnabled && Destination.IsAvailable
                && Building.isActiveAndEnabled && Building.workerPoint != null
                && (selectingTarget ? chosen.isActiveAndEnabled && !Building.HasWorker && Building.assignedWorker == null
                    : Building.HasWorker && Building.assignedWorker != null && Building.assignedWorker.isActiveAndEnabled);
            string text = selectingTarget ? "Presunúť sem" : "Presunúť pracovníka";
            if (MoveButtonText.text != text)
                MoveButtonText.text = text;
        }


        private void OnMoveClicked()
        {
            RefreshButtons();
            if (!MoveButton.interactable)
                return;

            if (Workers.chosenWorker == null)
                Workers.ChooseWorker(Building.assignedWorker);
            else
                Workers.TryAssignWorker(Building, Workers.chosenWorker);
        }
    }
}
