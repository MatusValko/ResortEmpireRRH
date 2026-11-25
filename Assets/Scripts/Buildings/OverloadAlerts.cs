using System;

namespace ResortEmpire.Buildings
{
    // Decides which overloaded building the warning card shows. The player can dismiss a warning;
    // it stays hidden until that building's queue drops back under the threshold.
    public sealed class OverloadAlerts
    {
        private readonly bool[] _dismissed;


        public OverloadAlerts(int buildingCount)
        {
            if (buildingCount < 0)
                throw new ArgumentOutOfRangeException(nameof(buildingCount), buildingCount, "Building count must not be negative.");

            _dismissed = new bool[buildingCount];
        }


        // -1 when no warning is shown
        public int Current { get; private set; } = -1;


        // overloaded[i]: the queue is over the threshold; canHelp[i]: a worker move or a boost is possible there
        public int Refresh(bool[] overloaded, bool[] canHelp)
        {
            if (overloaded == null || canHelp == null || overloaded.Length != _dismissed.Length || canHelp.Length != _dismissed.Length)
                throw new ArgumentException("One state per building is required.");

            for (int i = 0; i < _dismissed.Length; i++)
            {
                if (!overloaded[i])
                    _dismissed[i] = false;
            }

            if (Current >= 0 && IsCandidate(Current, overloaded, canHelp))
                return Current;

            Current = -1;
            for (int i = 0; i < _dismissed.Length; i++)
            {
                if (IsCandidate(i, overloaded, canHelp))
                {
                    Current = i;

                    break;
                }
            }

            return Current;
        }


        public void DismissCurrent()
        {
            if (Current < 0)
                return;

            _dismissed[Current] = true;
            Current = -1;
        }


        public void Reset()
        {
            Array.Clear(_dismissed, 0, _dismissed.Length);
            Current = -1;
        }


        private bool IsCandidate(int index, bool[] overloaded, bool[] canHelp)
        {
            return overloaded[index] && canHelp[index] && !_dismissed[index];
        }
    }
}
