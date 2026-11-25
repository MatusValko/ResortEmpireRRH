using ResortEmpire.Buildings;
using ResortEmpire.Economy;
using UnityEngine;

namespace ResortEmpire.UI
{
    // Energy boost button of one building in the services panel. Shown only when a boost is offered or running.
    [DisallowMultipleComponent]
    public sealed class BuildingBoostButton : MonoBehaviour
    {
        [SerializeField] private EnergyBoostsBehaviour Boosts;

        [SerializeField] private ServiceBuildingBehaviour Building;

        [SerializeField] private UnityEngine.UI.Button BoostButton;

        [SerializeField] private TMPro.TMP_Text Label;

        [Tooltip("Short texts for the small badge next to the building on the map.")]
        [SerializeField] private bool Compact;


        private bool _initialized;

        private BoostOffer _shownOffer = (BoostOffer)(-1);

        private int _shownSeconds = -1;


        private void Start()
        {
            if (Boosts == null || Building == null || BoostButton == null || Label == null)
            {
                Debug.LogError($"{nameof(BuildingBoostButton)} on '{name}': Assign boosts, building, button and label references.", this);
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
            if (BoostButton == null)
                return;

            BoostButton.onClick.RemoveListener(OnClicked);
            BoostButton.gameObject.SetActive(false);
        }


        private void LateUpdate()
        {
            Refresh();
        }


        private void Subscribe()
        {
            BoostButton.onClick.RemoveListener(OnClicked);
            BoostButton.onClick.AddListener(OnClicked);
            Refresh();
        }


        private void Refresh()
        {
            BoostOffer offer = Boosts.GetOffer(Building);
            bool visible = offer != BoostOffer.None;
            if (BoostButton.gameObject.activeSelf != visible)
                BoostButton.gameObject.SetActive(visible);

            BoostButton.interactable = offer == BoostOffer.Boost || offer == BoostOffer.WatchAd;

            int seconds = offer == BoostOffer.Running ? Mathf.CeilToInt(Boosts.GetRemainingSeconds(Building)) : 0;
            if (offer == _shownOffer && seconds == _shownSeconds)
                return;

            _shownOffer = offer;
            _shownSeconds = seconds;
            Label.SetText(offer switch
            {
                BoostOffer.Boost => Compact ? $"{Boosts.BoostCost}" : $"Zrýchliť · {Boosts.BoostCost}",
                BoostOffer.WatchAd => Compact ? $"+{Boosts.AdEnergy}" : $"Reklama +{Boosts.AdEnergy}",
                BoostOffer.Running => Compact ? $"{seconds}s" : $"{seconds} s",
                _ => string.Empty
            });
        }


        private void OnClicked()
        {
            switch (Boosts.GetOffer(Building))
            {
                case BoostOffer.Boost:
                    Boosts.TryBoost(Building);

                    break;

                case BoostOffer.WatchAd:
                    Boosts.TryWatchAd();

                    break;
            }

            Refresh();
        }
    }
}
