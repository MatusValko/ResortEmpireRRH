using System;
using System.Collections.Generic;

using ResortEmpire.Services;
using ResortEmpire.Waves;

namespace ResortEmpire.Requests
{
    public sealed class RequestQueues
    {
        private readonly IWaveContext _wave;

        private readonly Func<ServiceType, QueuedRequest, bool> _tryDeliver;

        private readonly Dictionary<ServiceType, Queue<QueuedRequest>> _waiting = new();

        private readonly HashSet<long> _receivedIds = new();

        private readonly HashSet<ServiceType> _dispatching = new();

        private readonly Dictionary<long, RequestServiceRecord> _serviced = new();


        public int WaveNumber { get; private set; }

        public long TotalWaiting { get; private set; }

        public long TotalInService { get; private set; }

        public long TotalCompleted { get; private set; }

        public long TotalUnfinished => TotalWaiting + TotalInService;

        public event Action<RequestServiceRecord> RequestStarted;

        public event Action<RequestServiceRecord> RequestCompleted;


        public RequestQueues(IWaveContext wave, Func<ServiceType, QueuedRequest, bool> tryDeliver)
        {
            _wave = wave ?? throw new ArgumentNullException(nameof(wave));
            _tryDeliver = tryDeliver ?? throw new ArgumentNullException(nameof(tryDeliver));

            foreach (ServiceType service in Enum.GetValues(typeof(ServiceType)))
                _waiting.Add(service, new Queue<QueuedRequest>());
        }


        public bool TryBeginWave()
        {
            if (_dispatching.Count != 0 || TotalUnfinished != 0 || _wave.State != WaveState.WaveActive
                || _wave.ElapsedSeconds != 0 || _wave.CurrentWaveNumber <= WaveNumber)
                return false;

            _receivedIds.Clear();
            _serviced.Clear();
            TotalCompleted = 0;
            WaveNumber = _wave.CurrentWaveNumber;

            return true;
        }


        public bool TryEnqueue(RequestArrival request)
        {
            if (!IsActiveWave() || request.WaveNumber != WaveNumber || request.Id <= 0)
                return false;

            Queue<QueuedRequest> queue = GetQueue(request.Service);
            double now = _wave.ElapsedSeconds;
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0)
                throw new InvalidOperationException("Waiting must begin at a finite non-negative wave time.");

            if (!_receivedIds.Add(request.Id))
                return false;

            queue.Enqueue(new QueuedRequest(request, now));
            TotalWaiting++;
            Dispatch(request.Service);

            return true;
        }


        public int GetWaitingCount(ServiceType service)
        {
            return GetQueue(service).Count;
        }


        public bool TryPeekWaiting(ServiceType service, out QueuedRequest request)
        {
            return GetQueue(service).TryPeek(out request);
        }


        public int Dispatch(ServiceType service)
        {
            Queue<QueuedRequest> queue = GetQueue(service);
            if (!IsActiveWave() || !_dispatching.Add(service))
                return 0;

            int delivered = 0;
            try
            {
                while (IsActiveWave() && queue.TryPeek(out QueuedRequest request))
                {
                    double startedAt = _wave.ElapsedSeconds;
                    if (!_tryDeliver(service, request))
                        break;

                    queue.Dequeue();
                    TotalWaiting--;
                    RequestServiceRecord record = new(request, startedAt);
                    _serviced.Add(request.Request.Id, record);
                    TotalInService++;
                    delivered++;
                    RequestStarted?.Invoke(record);
                }
            }
            finally
            {
                _dispatching.Remove(service);
            }

            return delivered;
        }


        public bool TryGetServiceRecord(long requestId, out RequestServiceRecord record)
        {
            return _serviced.TryGetValue(requestId, out record);
        }


        public bool TryCompleteRequest(RequestArrival request)
        {
            if (!IsActiveWave() || request.WaveNumber != WaveNumber
                || !_serviced.TryGetValue(request.Id, out RequestServiceRecord record)
                || record.IsCompleted || record.Queued.Request.Service != request.Service)
                return false;

            RequestServiceRecord completed = new(record.Queued, record.ServiceStartedSeconds, true);
            _serviced[request.Id] = completed;
            TotalInService--;
            TotalCompleted++;
            RequestCompleted?.Invoke(completed);

            return true;
        }


        private Queue<QueuedRequest> GetQueue(ServiceType service)
        {
            if (!_waiting.TryGetValue(service, out Queue<QueuedRequest> queue))
                throw new ArgumentOutOfRangeException(nameof(service));

            return queue;
        }


        private bool IsActiveWave()
        {
            return WaveNumber != 0 && _wave.CurrentWaveNumber == WaveNumber && _wave.State == WaveState.WaveActive;
        }
    }
}
