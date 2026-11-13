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

        [SerializeField] private ServiceBuilding Building;

        [SerializeField] private string DisplayName;

        [SerializeField] private UnityEngine.UI.Button MoveButton;

        [SerializeField] private TMPro.TMP_Text MoveButtonText;

        [SerializeField] private UnityEngine.UI.Button UpgradeButton;


        private bool _initialized;


        private void OnEnable()
        {
            if (_initialized)
                Subscribe();
        }


        private void Start()
        {
            if (Windows == null || Workers == null || Destination == null || Building == null
                || MoveButton == null || MoveButtonText == null || UpgradeButton == null)
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

            if (UpgradeButton != null)
            {
                UpgradeButton.onClick.RemoveListener(OnUpgradeClicked);
                UpgradeButton.interactable = false;
            }
        }


        private void Subscribe()
        {
            MoveButton.onClick.AddListener(OnMoveClicked);
            UpgradeButton.onClick.AddListener(OnUpgradeClicked);
            RefreshButtons();
        }


        private void RefreshButtons()
        {
            Worker chosen = Workers.chosenWorker;
            bool selectingTarget = chosen != null;
            MoveButton.interactable = Windows.CanMoveWorkers && Workers.isActiveAndEnabled && Destination.IsAvailable
                && (selectingTarget ? !Building.HasWorker && chosen.assignedBuilding != null
                    : Building.HasWorker && Building.assignedWorker != null);
            string text = selectingTarget ? "Presunúť sem" : "Presunúť pracovníka";
            if (MoveButtonText.text != text)
                MoveButtonText.text = text;

            UpgradeButton.gameObject.SetActive(Windows.CanOpenPreviews && Destination.IsAvailable);
            UpgradeButton.interactable = Windows.CanOpenPreviews && Destination.IsAvailable;
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


        private void OnUpgradeClicked()
        {
            if (Destination.IsAvailable)
                Windows.ShowUpgrade(DisplayName);
        }
    }
}
