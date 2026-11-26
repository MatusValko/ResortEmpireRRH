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


        internal void Begin(IWaveContext wave, Vector3 shore, Vector3 stagingPosition,
            Action onAshore, Action<CustomerWalk> onReady)
        {
            _wave = wave;
            StartCoroutine(FollowRoute(shore, stagingPosition, onAshore, onReady));
        }


        internal void SendToBuilding(long requestId, Transform[] path, BuildingQueue queue)
        {
            RequestId = requestId;
            _path = path;
            _queue = queue;
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


        private IEnumerator FollowRoute(Vector3 shore, Vector3 stagingPosition, Action onAshore, Action<CustomerWalk> onReady)
        {
            SetWalking(true);
            yield return MoveTo(shore);
            IsAshore = true;
            onAshore();
            yield return MoveTo(stagingPosition);

            SetWalking(false);
            onReady(this);
            while (_queueSlot == null || _wave.State == WaveState.WavePaused)
            {
                Animator.speed = _wave.State == WaveState.WavePaused ? 0f : 1f;
                yield return null;
            }

            SetWalking(true);
            // The last waypoint is the entrance, reached only after the queue.
            for (int i = 0; i < _path.Length - 1; i++)
                yield return MoveTo(_path[i].position);

            SetWalking(false);
            while (_wave.State == WaveState.WavePaused || !_queue.CanEnterLane(this))
            {
                Animator.speed = _wave.State == WaveState.WavePaused ? 0f : 1f;
                yield return null;
            }

            IsOnQueueLane = true;
            yield return FollowQueue();

            SetWalking(true);
            yield return MoveTo(_path[_path.Length - 1].position);
            yield return MoveTo(_servicePoint.position);

            IsAtBuilding = true;
            Animator.gameObject.SetActive(false);
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


        private IEnumerator MoveTo(Vector3 destination)
        {
            while ((destination - transform.position).sqrMagnitude > 0.0001f)
            {
                bool paused = _wave.State == WaveState.WavePaused;
                Animator.speed = paused ? 0f : 1f;
                if (!paused)
                    StepTowards(destination);

                yield return null;
            }

            transform.position = destination;
        }


        private void StepTowards(Vector3 destination)
        {
            Vector3 direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, RotationSpeed * Time.deltaTime);
            }

            transform.position = Vector3.MoveTowards(transform.position, destination, MoveSpeed * Time.deltaTime);
        }


        private void SetWalking(bool walking)
        {
            Animator.SetBool(Idle, !walking);
            Animator.SetBool(Walk, walking);
        }
    }
}
