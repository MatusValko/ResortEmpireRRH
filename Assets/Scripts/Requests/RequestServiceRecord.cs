namespace ResortEmpire.Requests
{
    public readonly struct RequestServiceRecord
    {
        public QueuedRequest Queued { get; }

        public double ServiceStartedSeconds { get; }

        public double WaitingDurationSeconds => ServiceStartedSeconds - Queued.WaitingSinceSeconds;

        public bool IsCompleted { get; }


        internal RequestServiceRecord(QueuedRequest queued, double serviceStartedSeconds, bool isCompleted = false)
        {
            Queued = queued;
            ServiceStartedSeconds = serviceStartedSeconds;
            IsCompleted = isCompleted;
        }
    }
}
