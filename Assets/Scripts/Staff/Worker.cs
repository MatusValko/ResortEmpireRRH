using System.Collections;
using ResortEmpire.Buildings;
using UnityEngine;

public class Worker : MonoBehaviour
{
    public enum WorkerState { idle, work, walk }

    public ServiceBuildingBehaviour assignedBuilding;
    public ServiceBuildingBehaviour targetBuilding;
    public bool IsWalking => targetBuilding != null;
    public float moveSpeed = 5f;
    public float rotationSpeed = 720f;

    [SerializeField] private WorkerState state = WorkerState.idle;
    private Coroutine moveCoroutine;
    private Animator animator;
    private CapsuleCollider _body;

    private void Awake()
    {
        state = WorkerState.idle;
        animator = GetComponentInChildren<Animator>();
        _body = GetComponent<CapsuleCollider>();
    }

    internal Vector3 GetStandingPosition(ServiceBuildingBehaviour building)
    {
        Vector3 position = building.GetWorkerPosition();
        if (_body != null)
            position.y += transform.position.y - _body.bounds.min.y;

        return position;
    }

    public void MoveToBuilding(ServiceBuildingBehaviour building)
    {
        if (building == null || building.workerPoint == null)
            return;

        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            state = WorkerState.idle;
        }

        targetBuilding = building;
        moveCoroutine = StartCoroutine(SmoothMove(building));
    }

    private IEnumerator SmoothMove(ServiceBuildingBehaviour building)
    {
        state = WorkerState.walk;
        Vector3 targetPosition = GetStandingPosition(building);

        while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion moveRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, moveRotation, rotationSpeed * Time.deltaTime);
            }

            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            yield return null;
        }

        transform.position = targetPosition;

        assignedBuilding = building;
        building.HasWorker = true;
        building.assignedWorker = this;
        targetBuilding = null;
        Quaternion finalRotation = building.workerPoint.rotation;
        while (Quaternion.Angle(transform.rotation, finalRotation) > 0.5f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, finalRotation, rotationSpeed * Time.deltaTime);
            yield return null;
        }

        transform.rotation = finalRotation;
        moveCoroutine = null;
        state = WorkerState.idle;
    }

    void Update()
    {
        if(state == WorkerState.walk)
        {
            animator.SetBool("Walk", true);
            animator.SetBool("Idle", false);
        }else{
             animator.SetBool("Walk", false);
            animator.SetBool("Idle", true);
        } 
    }
}
