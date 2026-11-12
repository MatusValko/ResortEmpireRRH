using System;

using ResortEmpire.Services;

namespace ResortEmpire.Requests
{
    public sealed class ServiceRequest
    {
        public ServiceType Service { get; }

        public double ArrivedAtSeconds { get; }

        public double SeatedAtSeconds { get; private set; } = -1;

        public bool IsSeated => SeatedAtSeconds >= 0;

        public double WaitSeconds => IsSeated ? (SeatedAtSeconds - ArrivedAtSeconds) : -1f;


        public ServiceRequest(ServiceType service, double arrivedAtSeconds)
        {
            if (!Enum.IsDefined(typeof(ServiceType), service))
                throw new ArgumentOutOfRangeException(nameof(service), service, "Unknown service type.");

            if (double.IsNaN(arrivedAtSeconds) || double.IsInfinity(arrivedAtSeconds) || arrivedAtSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(arrivedAtSeconds), arrivedAtSeconds, "Arrival time must be finite and not negative.");

            Service = service;
            ArrivedAtSeconds = arrivedAtSeconds;
        }


        public void MarkSeated(double seatedAtSeconds)
        {
            if (IsSeated)
                throw new InvalidOperationException("The request has already been seated.");

            if (seatedAtSeconds < ArrivedAtSeconds)
                throw new ArgumentOutOfRangeException(nameof(seatedAtSeconds), seatedAtSeconds, "A request cannot be seated before it arrives.");

            SeatedAtSeconds = seatedAtSeconds;
        }
    }
}