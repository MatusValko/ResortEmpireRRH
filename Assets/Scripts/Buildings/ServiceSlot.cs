using System;

using ResortEmpire.Requests;

namespace ResortEmpire.Buildings
{
    public sealed class ServiceSlot
    {
        public ServiceRequest Request { get; private set; }

        public float Progress { get; private set; }

        // Progress earned beyond the finished service, waiting for the next request
        public float CarriedProgress { get; private set; }

        public bool IsFree => Request == null;

        public bool IsFinished => Request != null && Progress >= 1f;


        public void Occupy(ServiceRequest request)
        {
            if (!IsFree)
                throw new InvalidOperationException("The slot is already occupied.");

            Request = request ?? throw new ArgumentNullException(nameof(request));
            Progress = CarriedProgress;
            CarriedProgress = 0f;
        }


        public void Advance(float amount)
        {
            if (IsFree)
                throw new InvalidOperationException("An empty slot cannot advance.");

            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Progress must be finite and must not move backwards.");

            Progress += amount;
        }


        public ServiceRequest Release()
        {
            if (IsFree)
                throw new InvalidOperationException("The slot is already free.");

            ServiceRequest released = Request;

            CarriedProgress = IsFinished ? Progress - 1f : 0f;
            Request = null;
            Progress = 0f;

            return released;
        }
    }
}
