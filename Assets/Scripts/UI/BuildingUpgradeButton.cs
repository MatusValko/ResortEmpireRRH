using ResortEmpire.Buildings;
using ResortEmpire.Services;
using UnityEngine;

namespace ResortEmpire.UI
{
    // Opens the upgrade window for one building. Upgrades happen only between waves,
    // so the button is hidden while a wave runs or another window is open.
    [DisallowMultipleComponent]
    public sealed class BuildingUpgradeButton : MonoBehaviour
    {
        [SerializeField] private ResortWindows Windows;

        [SerializeField] private ServiceDestination Destination;

        [SerializeField] private ServiceBuildingBehaviour Building;

        [SerializeField] private string DisplayName;

        [SerializeField] private UnityEngine.UI.Button UpgradeButton;


        private bool _initialized;


        private void Start()
        {
            if (Windows == null || Destination == null || Building == null || UpgradeButton == null)
            {
                Debug.LogError($"{nameof(BuildingUpgradeButton)} on '{name}': Assign windows, building and button references.", this);
                enabled = false;

                return;
            }

            _initialized = true;
            Subscribe();
        }


        private void OnEnable()
        {
            if (_initialized)
                Subscribe();
        }


        private void OnDisable()
        {
            if (UpgradeButton == null)
                return;

            UpgradeButton.onClick.RemoveListener(OnClicked);
            UpgradeButton.interactable = false;
        }


        private void LateUpdate()
        {
            Refresh();
        }


        private void Subscribe()
        {
            UpgradeButton.onClick.RemoveListener(OnClicked);
            UpgradeButton.onClick.AddListener(OnClicked);
            Refresh();
        }


        private void Refresh()
        {
            bool canOpen = Windows.CanOpenPreviews && Destination.IsAvailable && Building.Building != null;
            if (UpgradeButton.gameObject.activeSelf != canOpen)
                UpgradeButton.gameObject.SetActive(canOpen);

            UpgradeButton.interactable = canOpen;
        }


        private void OnClicked()
        {
            if (Destination.IsAvailable && Building.Building != null)
                Windows.ShowUpgrade(Building.Building, DisplayName);
        }
    }
}
