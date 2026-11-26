using System.Collections.Generic;

using ResortEmpire.Buildings;
using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.Requests
{
    internal sealed class BuildingQueue
    {
        private readonly Transform[] _slots;

        private readonly Transform _servicePoint;

        private readonly ServiceBuildingBehaviour _building;

        private readonly float _spacingSquared;

        private readonly List<(RequestArrival Request, CustomerWalk Customer)> _line = new();

        private readonly Queue<(RequestArrival Request, CustomerWalk Customer)> _overflow = new();

        private readonly Queue<(QueuedRequest Request, CustomerWalk Customer, double ReservedAt)> _reserved = new();


        internal ServiceDestination Destination { get; }


        internal BuildingQueue(ServiceDestination destination, Transform[] slots, Transform servicePoint)
        {
            Destination = destination;
            _slots = slots;
            _servicePoint = servicePoint;
            _building = destination != null ? destination.GetComponent<ServiceBuildingBehaviour>() : null;
            _spacingSquared = slots.Length > 1 ? (slots[1].position - slots[0].position).sqrMagnitude : 0f;
        }


        internal void Add(RequestArrival request, CustomerWalk customer)
        {
            _overflow.Enqueue((request, customer));
            AdmitNext();
        }


        internal int GetWaitingCount()
        {
            int count = 0;
            foreach (var entry in _line)
            {
                if (entry.Customer.WaitingSinceSeconds.HasValue)
                    count++;
            }

            return count;
        }


        internal bool TryGetOldest(out QueuedRequest oldest)
        {
            oldest = default;
            double earliest = double.PositiveInfinity;
            foreach (var entry in _line)
            {
                if (entry.Customer.WaitingSinceSeconds is double waitingSince && waitingSince < earliest)
                {
                    earliest = waitingSince;
                    oldest = new QueuedRequest(entry.Request, waitingSince);
                }
            }

            return earliest < double.PositiveInfinity;
        }


        internal bool CanEnterLane(CustomerWalk customer)
        {
            for (int i = 0; i < _line.Count; i++)
            {
                if (_line[i].Customer != customer)
                    continue;

                if (i == 0)
                    return true;

                CustomerWalk predecessor = _line[i - 1].Customer;
                return predecessor.IsOnQueueLane
                    && (predecessor.transform.position - customer.transform.position).sqrMagnitude >= _spacingSquared;
            }

            return false;
        }


        internal bool TryGetReadyHead(out QueuedRequest request)
        {
            request = default;
            if (_line.Count == 0)
                return false;

            var head = _line[0];
            if (!head.Customer.IsAtQueueSlot || head.Customer.WaitingSinceSeconds is not double waitingSince)
                return false;

            request = new QueuedRequest(head.Request, waitingSince);
            return true;
        }


        internal void ReserveHead(QueuedRequest request, double reservedAt)
        {
            if (!Destination.IsAvailable || !_building.isActiveAndEnabled || !_building.Building.TryReserveService(request))
                return;

            CustomerWalk first = _line[0].Customer;
            _line.RemoveAt(0);
            _reserved.Enqueue((request, first, reservedAt));
            first.GoToServicePoint(_servicePoint);

            for (int i = 0; i < _line.Count; i++)
                _line[i].Customer.MoveToQueueSlot(_slots[i]);

            AdmitNext();
        }


        internal bool TryStartArrivedGuest(out QueuedRequest request, out double reservedAt)
        {
            request = default;
            reservedAt = 0d;
            if (!_reserved.TryPeek(out var arrival) || !arrival.Customer.IsAtBuilding
                || !Destination.IsAvailable || !_building.isActiveAndEnabled
                || !_building.Building.TryStartReservedService(arrival.Request.Request.Id))
                return false;

            _reserved.Dequeue();
            request = arrival.Request;
            reservedAt = arrival.ReservedAt;
            return true;
        }


        internal void Clear()
        {
            _line.Clear();
            _overflow.Clear();
            _reserved.Clear();
        }


        private void AdmitNext()
        {
            if (_line.Count == _slots.Length || !_overflow.TryDequeue(out var entry))
                return;

            entry.Customer.MoveToQueueSlot(_slots[_line.Count]);
            _line.Add(entry);
        }
    }
}
