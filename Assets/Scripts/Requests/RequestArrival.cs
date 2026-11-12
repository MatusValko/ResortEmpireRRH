using ResortEmpire.Services;

namespace ResortEmpire.Requests
{
    public readonly struct RequestArrival
    {
        public long Id { get; }

        public int WaveNumber { get; }

        public ServiceType Service { get; }

        public double ScheduledTimeSeconds { get; }


        internal RequestArrival(long id, int waveNumber, ServiceType service, double scheduledTimeSeconds)
        {
            Id = id;
            WaveNumber = waveNumber;
            Service = service;
            ScheduledTimeSeconds = scheduledTimeSeconds;
        }
    }
}
