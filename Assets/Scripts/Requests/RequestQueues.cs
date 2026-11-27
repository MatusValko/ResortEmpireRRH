using System;
using System.Collections.Generic;

using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEngine;

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

        private readonly Dictionary<ServiceDestination, BuildingQueue> _buildingQueues = new();

        private readonly HashSet<long> _incoming = new();

        private long _logicalWaiting;


        public int WaveNumber { get; private set; }

        public long TotalWaiting
        {
            get
            {
                long count = _logicalWaiting;
                foreach (BuildingQueue queue in _buildingQueues.Values)
                    count += queue.GetWaitingCount();

                return count;
            }
        }

        public long TotalInService { get; private set; }

        public long TotalCompleted { get; private set; }

        public long TotalUnfinished => _logicalWaiting + _incoming.Count + TotalInService;

        internal bool HasBuildingQueues => _buildingQueues.Count != 0;

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
            _logicalWaiting++;
            Dispatch(request.Service);

            return true;
        }


        public int GetWaitingCount(ServiceType service)
        {
            int count = GetQueue(service).Count;
            foreach (BuildingQueue queue in _buildingQueues.Values)
            {
                if (queue.Destination.Service == service)
                    count += queue.GetWaitingCount();
            }

            return count;
        }


        public int GetWaitingCount(ServiceDestination destination)
        {
            return _buildingQueues.TryGetValue(destination, out BuildingQueue queue)
                ? queue.GetWaitingCount()
                : GetQueue(destination.Service).Count;
        }


        public int GetCompletedCount(ServiceType service)
        {
            if (!_waiting.ContainsKey(service))
                throw new ArgumentOutOfRangeException(nameof(service));

            int count = 0;
            foreach (RequestServiceRecord record in _serviced.Values)
            {
                if (record.IsCompleted && record.Queued.Request.Service == service)
                    count++;
            }

            return count;
        }


        public bool TryPeekWaiting(ServiceType service, out QueuedRequest request)
        {
            bool found = GetQueue(service).TryPeek(out request);
            foreach (BuildingQueue queue in _buildingQueues.Values)
            {
                if (queue.Destination.Service == service && queue.TryGetOldest(out QueuedRequest candidate)
                    && (!found || candidate.WaitingSinceSeconds < request.WaitingSinceSeconds))
                {
                    found = true;
                    request = candidate;
                }
            }

            return found;
        }


        public bool TryPeekWaiting(ServiceDestination destination, out QueuedRequest request)
        {
            return _buildingQueues.TryGetValue(destination, out BuildingQueue queue)
                ? queue.TryGetOldest(out request)
                : GetQueue(destination.Service).TryPeek(out request);
        }


        internal bool TryGetWaitingHead(ServiceDestination destination, out Transform guest, out double waitingSince)
        {
            if (_buildingQueues.TryGetValue(destination, out BuildingQueue queue))
                return queue.TryGetWaitingHead(out guest, out waitingSince);

            guest = null;
            waitingSince = 0d;
            return false;
        }


        internal BuildingQueue RegisterBuilding(ServiceDestination destination, Transform[] slots, Transform servicePoint)
        {
            BuildingQueue queue = new(destination, slots, servicePoint);
            _buildingQueues.Add(destination, queue);
            return queue;
        }


        internal bool TryTrackIncoming(RequestArrival request)
        {
            if (!IsActiveWave() || request.WaveNumber != WaveNumber || request.Id <= 0 || !_receivedIds.Add(request.Id))
                return false;

            return _incoming.Add(request.Id);
        }


        internal void DispatchBuildingQueues()
        {
            foreach (BuildingQueue queue in _buildingQueues.Values)
            {
                if (!IsActiveWave())
                    return;

                while (IsActiveWave() && queue.TryStartArrivedGuest(out QueuedRequest arrived, out double reservedAt))
                {
                    _incoming.Remove(arrived.Request.Id);
                    RecordStarted(arrived, _wave.ElapsedSeconds, reservedAt);
                }

                if (IsActiveWave() && queue.TryGetReadyHead(out QueuedRequest head) && _incoming.Contains(head.Request.Id))
                    queue.ReserveHead(head, _wave.ElapsedSeconds);
            }
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
                    _logicalWaiting--;
                    delivered++;
                    RecordStarted(request, startedAt);
                }
            }
            finally
            {
                _dispatching.Remove(service);
            }

            return delivered;
        }


        private void RecordStarted(QueuedRequest request, double startedAt, double? waitingEndedAt = null)
        {
            RequestServiceRecord record = new(request, startedAt, false, waitingEndedAt ?? startedAt);
            _serviced.Add(request.Request.Id, record);
            TotalInService++;
            RequestStarted?.Invoke(record);
        }


        public bool TryGetServiceRecord(long requestId, out RequestServiceRecord record)
        {
            return _serviced.TryGetValue(requestId, out record);
        }


        public IEnumerable<RequestServiceRecord> GetCompletedRecords()
        {
            foreach (RequestServiceRecord record in _serviced.Values)
            {
                if (record.IsCompleted)
                    yield return record;
            }
        }


        public bool TryCompleteRequest(RequestArrival request)
        {
            if (!IsActiveWave() || request.WaveNumber != WaveNumber
                || !_serviced.TryGetValue(request.Id, out RequestServiceRecord record)
                || record.IsCompleted || record.Queued.Request.Service != request.Service)
                return false;

            RequestServiceRecord completed = new(record.Queued, record.ServiceStartedSeconds, true, record.WaitingEndedSeconds);
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
