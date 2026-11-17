using System.Collections;
using ResortEmpire.Buildings;
using UnityEngine;

public class Worker : MonoBehaviour
{
    public enum WorkerState { idle, work, walk }

    public ServiceBuildingBehaviour assignedBuilding;
    public ServiceBuildingBehaviour targetBuilding;
    public float moveSpeed = 5f;
    public float rotationSpeed = 720f;

    [SerializeField] private WorkerState state = WorkerState.idle;
    private Coroutine moveCoroutine;
    private Animator animator;

    private void Awake()
    {
        state = WorkerState.idle;
        animator = GetComponentInChildren<Animator>();
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
        Vector3 targetPosition = building.workerPoint.position;

        while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            targetPosition = building.workerPoint.position;
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
        Quaternion finalRotation = Quaternion.Euler(0f, 180f, 0f);
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