using UnityEngine;

namespace ResortEmpire.Services
{
    [DisallowMultipleComponent]
    public sealed class ServiceDestination : MonoBehaviour
    {
        [SerializeField] private ServiceType ProvidedService;


        public ServiceType Service => ProvidedService;
    }
}
