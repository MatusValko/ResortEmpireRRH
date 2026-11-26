using System;

using ResortEmpire.Requests;

namespace ResortEmpire.Buildings
{
    public sealed class ServiceSlot
    {
        private QueuedRequest _request;


        // QueuedRequest is a struct, so occupancy is tracked by a flag instead of null
        public bool IsFree { get; private set; } = true;

        public bool IsReserved { get; private set; }

        public QueuedRequest Request => IsFree
            ? throw new InvalidOperationException("The slot is free.")
            : _request;

        public float Progress { get; private set; }

        // Progress earned beyond the finished service, waiting for the next request
        public float CarriedProgress { get; private set; }

        public bool IsFinished => !IsFree && !IsReserved && Progress >= 1f;


        public void Occupy(QueuedRequest request)
        {
            if (!IsFree)
                throw new InvalidOperationException("The slot is already occupied.");

            _request = request;
            IsFree = false;
            Progress = CarriedProgress;
            CarriedProgress = 0f;
        }


        internal void Reserve(QueuedRequest request)
        {
            Occupy(request);
            // Walking to the seat is outside the service timer, including any leftover progress.
            Progress = 0f;
            IsReserved = true;
        }


        internal void StartReserved()
        {
            if (!IsReserved)
                throw new InvalidOperationException("The slot is not reserved.");

            IsReserved = false;
        }


        public void Advance(float amount)
        {
            if (IsFree)
                throw new InvalidOperationException("An empty slot cannot advance.");
            if (IsReserved)
                throw new InvalidOperationException("A reserved slot cannot advance before the guest arrives.");

            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Progress must be finite and must not move backwards.");

            Progress += amount;
        }


        public QueuedRequest Release()
        {
            if (IsFree)
                throw new InvalidOperationException("The slot is already free.");

            QueuedRequest released = _request;

            CarriedProgress = IsFinished ? Progress - 1f : 0f;
            _request = default;
            IsFree = true;
            IsReserved = false;
            Progress = 0f;

            return released;
        }
    }
}
