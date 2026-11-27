using System;
using System.Collections;

using ResortEmpire.Requests;
using UnityEngine;

namespace ResortEmpire.Waves
{
    [DisallowMultipleComponent]
    public sealed class CustomerWalk : MonoBehaviour
    {
        [SerializeField] private Animator Animator;

        [SerializeField, Min(0.1f)] private float MoveSpeed = 2f;

        [SerializeField, Min(1f)] private float RotationSpeed = 720f;


        private static readonly int Idle = UnityEngine.Animator.StringToHash("Idle");
        private static readonly int Walk = UnityEngine.Animator.StringToHash("Walk");

        private IWaveContext _wave;

        private Transform[] _path;

        private Vector3[] _approach;

        private Vector3[] _serviceApproach;

        private BuildingQueue _queue;

        private Transform _queueSlot;

        private Transform _servicePoint;


        internal bool IsAshore { get; private set; }

        internal bool IsAtBuilding { get; private set; }

        internal long RequestId { get; private set; }

        internal bool IsOnQueueLane { get; private set; }

        internal double? WaitingSinceSeconds { get; private set; }

        internal bool IsAtQueueSlot => IsOnQueueLane && _queueSlot != null
            && (transform.position - _queueSlot.position).sqrMagnitude <= 0.0001f;


        internal void Begin(IWaveContext wave, Vector3 shore, Vector3? shoreAccess, ShoreWaitingArea.Spot spot,
            Action<CustomerWalk> onAshore, Action<CustomerWalk> onReady)
        {
            _wave = wave;
            StartCoroutine(FollowRoute(shore, shoreAccess, spot, onAshore, onReady));
        }


        internal void SendToBuilding(long requestId, Transform[] path, BuildingQueue queue,
            Vector3[] approach, Vector3[] serviceApproach)
        {
            RequestId = requestId;
            _path = path;
            _queue = queue;
            _approach = approach;
            _serviceApproach = serviceApproach;
        }


        internal void MoveToQueueSlot(Transform slot)
        {
            _queueSlot = slot;
        }


        internal void GoToServicePoint(Transform servicePoint)
        {
            _servicePoint = servicePoint;
            _queueSlot = null;
            WaitingSinceSeconds = null;
        }


        private IEnumerator FollowRoute(Vector3 shore, Vector3? shoreAccess, ShoreWaitingArea.Spot spot,
            Action<CustomerWalk> onAshore, Action<CustomerWalk> onReady)
        {
            SetWalking(true);
            yield return MoveTo(shore);
            IsAshore = true;
            onAshore(this);
            if (_queueSlot == null)
                onReady(this);

            Vector3 sideOffset = Vector3.zero;
            if (spot != null && _queueSlot == null)
            {
                sideOffset = spot.SideOffset;
                if (shoreAccess.HasValue)
                    yield return MoveTo(shoreAccess.Value + sideOffset);

                yield return MoveTo(spot.Access);
                yield return MoveTo(spot.Position);
                yield return WaitOnShore(spot.Position);
                yield return MoveTo(spot.Access);
            }
            else if (shoreAccess.HasValue)
            {
                yield return MoveTo(shoreAccess.Value);
            }

            if (spot != null)
                spot.InUse = false;

            SetWalking(true);
            if (_approach != null)
            {
                yield return MoveTo(_approach[0] + sideOffset);
                for (int i = 1; i < _approach.Length; i++)
                    yield return MoveTo(_approach[i]);
            }
            else
            {
                for (int i = 0; i < _path.Length - 1; i++)
                    yield return MoveTo(_path[i].position);
            }

            SetWalking(false);
            while (_wave.State == WaveState.WavePaused || !_queue.CanEnterLane(this))
            {
                Animator.speed = _wave.State == WaveState.WavePaused ? 0f : 1f;
                yield return null;
            }

            IsOnQueueLane = true;
            yield return FollowQueue();

            SetWalking(true);
            if (_serviceApproach != null)
            {
                foreach (Vector3 point in _serviceApproach)
                    yield return MoveTo(point);
            }
            else
            {
                yield return MoveTo(_path[^1].position);
                yield return MoveTo(_servicePoint.position);
            }

            IsAtBuilding = true;
            Animator.gameObject.SetActive(false);
        }


        private IEnumerator WaitOnShore(Vector3 home)
        {
            while (_queueSlot == null)
            {
                SetWalking(false);
                double nextWalk = _wave.ElapsedSeconds + UnityEngine.Random.Range(1.5f, 4f);
                while (_queueSlot == null && _wave.ElapsedSeconds < nextWalk)
                {
                    Animator.speed = _wave.State == WaveState.WavePaused ? 0f : 1f;
                    yield return null;
                }

                if (_queueSlot != null)
                    break;

                Vector2 offset = UnityEngine.Random.insideUnitCircle * ShoreWaitingArea.WanderRadius;
                SetWalking(true);
                yield return MoveTo(home + new Vector3(offset.x, 0f, offset.y), 0.38f);
            }

            SetWalking(true);
            yield return MoveTo(home, 0.65f);
        }


        private IEnumerator FollowQueue()
        {
            bool walking = false;
            while (_servicePoint == null)
            {
                bool paused = _wave.State == WaveState.WavePaused;
                Animator.speed = paused ? 0f : 1f;
                if (!paused)
                {
                    bool moving = !IsAtQueueSlot;
                    if (moving != walking)
                    {
                        SetWalking(moving);
                        walking = moving;
                    }

                    if (moving)
                    {
                        StepTowards(_queueSlot.position);
                    }
                    else
                    {
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, _queueSlot.rotation, RotationSpeed * Time.deltaTime);
                        WaitingSinceSeconds ??= _wave.ElapsedSeconds;
                    }
                }

                yield return null;
            }
        }


        private IEnumerator MoveTo(Vector3 destination, float speedMultiplier = 1f)
        {
            while ((destination - transform.position).sqrMagnitude > 0.0001f)
            {
                bool paused = _wave.State == WaveState.WavePaused;
                Animator.speed = paused ? 0f : 1f;
                if (!paused)
                    StepTowards(destination, speedMultiplier);

                yield return null;
            }

            transform.position = destination;
        }


        private void StepTowards(Vector3 destination, float speedMultiplier = 1f)
        {
            Vector3 direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, RotationSpeed * Time.deltaTime);
            }

            transform.position = Vector3.MoveTowards(transform.position, destination, MoveSpeed * speedMultiplier * Time.deltaTime);
        }


        private void SetWalking(bool walking)
        {
            Animator.SetBool(Idle, !walking);
            Animator.SetBool(Walk, walking);
        }
    }
}
