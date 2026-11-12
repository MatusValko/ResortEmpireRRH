using System;

using ResortEmpire.Requests;
using UnityEngine;

namespace ResortEmpire.Services
{
    [DisallowMultipleComponent]
    public sealed class ServiceDestination : MonoBehaviour
    {
        [SerializeField] private ServiceType ProvidedService;

        [SerializeField] private MonoBehaviour Receiver;

        [SerializeField] private bool Unlocked = true;


        public ServiceType Service => ProvidedService;

        public bool IsUnlocked => Unlocked;

        public bool IsAvailable => Unlocked && isActiveAndEnabled;

        public event Action<ServiceType> CapacityAvailable;


        private void OnEnable()
        {
            NotifyCapacityAvailable();
        }


        private void OnValidate()
        {
            if (Receiver != null && Receiver is not IRequestReceiver)
                Debug.LogError($"{nameof(ServiceDestination)} on '{name}': Receiver must implement IRequestReceiver.", this);
        }


        public bool TryAccept(QueuedRequest request)
        {
            if (!IsAvailable || request.Request.Id <= 0 || request.Request.Service != ProvidedService
                || Receiver == null || !Receiver.isActiveAndEnabled)
                return false;

            if (Receiver is not IRequestReceiver receiver)
                throw new InvalidOperationException($"{nameof(ServiceDestination)} on '{name}': Receiver must implement IRequestReceiver.");

            return receiver.TryAccept(request);
        }


        public void SetUnlocked(bool unlocked)
        {
            if (Unlocked == unlocked)
                return;

            Unlocked = unlocked;
            if (Unlocked)
                NotifyCapacityAvailable();
        }


        // Buildings must call this when a service slot becomes available,
        // so Requests can dispatch the next queued request.
        public void NotifyCapacityAvailable()
        {
            if (IsAvailable)
                CapacityAvailable?.Invoke(ProvidedService);
        }
    }
}
