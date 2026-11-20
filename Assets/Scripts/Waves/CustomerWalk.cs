using System.Collections;

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
        private bool _requested;


        internal bool IsAshore { get; private set; }


        internal void Begin(IWaveContext wave, Vector3 shore, Vector3 stagingPosition, Transform[] path)
        {
            _wave = wave;
            StartCoroutine(FollowRoute(shore, stagingPosition, path));
        }


        internal void SendToBuilding()
        {
            _requested = true;
        }


        private IEnumerator FollowRoute(Vector3 shore, Vector3 stagingPosition, Transform[] path)
        {
            SetWalking(true);
            yield return MoveTo(shore);
            IsAshore = true;
            yield return MoveTo(stagingPosition);

            SetWalking(false);
            while (!_requested || _wave.State == WaveState.WavePaused)
            {
                Animator.speed = _wave.State == WaveState.WavePaused ? 0f : 1f;
                yield return null;
            }

            SetWalking(true);
            foreach (Transform point in path)
                yield return MoveTo(point.position);

            Destroy(gameObject);
        }


        private IEnumerator MoveTo(Vector3 destination)
        {
            while ((destination - transform.position).sqrMagnitude > 0.0001f)
            {
                bool paused = _wave.State == WaveState.WavePaused;
                Animator.speed = paused ? 0f : 1f;
                if (!paused)
                {
                    Vector3 direction = destination - transform.position;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(direction);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, RotationSpeed * Time.deltaTime);
                    }

                    transform.position = Vector3.MoveTowards(transform.position, destination, MoveSpeed * Time.deltaTime);
                }

                yield return null;
            }

            transform.position = destination;
        }


        private void SetWalking(bool walking)
        {
            Animator.SetBool(Idle, !walking);
            Animator.SetBool(Walk, walking);
        }
    }
}
