namespace ResortEmpire.Requests
{
    public readonly struct RequestServiceRecord
    {
        public QueuedRequest Queued { get; }

        public double ServiceStartedSeconds { get; }

        public double WaitingEndedSeconds { get; }

        public double WaitingDurationSeconds => WaitingEndedSeconds - Queued.WaitingSinceSeconds;

        public bool IsCompleted { get; }


        internal RequestServiceRecord(QueuedRequest queued, double serviceStartedSeconds, bool isCompleted, double waitingEndedAt)
        {
            Queued = queued;
            ServiceStartedSeconds = serviceStartedSeconds;
            WaitingEndedSeconds = waitingEndedAt;
            IsCompleted = isCompleted;
        }
    }
}
