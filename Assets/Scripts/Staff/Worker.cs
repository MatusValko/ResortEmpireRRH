using System.Collections;
using ResortEmpire.Buildings;
using UnityEngine;

public class Worker : MonoBehaviour
{
    public ServiceBuildingBehaviour assignedBuilding;
    public ServiceBuildingBehaviour targetBuilding;
    public float moveSpeed = 5f;

    private Coroutine moveCoroutine;

    public void MoveToBuilding(ServiceBuildingBehaviour building)
    {
        if (building == null || building.workerPoint == null)
            return;

        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }

        targetBuilding = building;
        moveCoroutine = StartCoroutine(SmoothMove(building));
    }

    private IEnumerator SmoothMove(ServiceBuildingBehaviour building)
    {
        Vector3 targetPosition = building.workerPoint.position;

        while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            targetPosition = building.workerPoint.position;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            yield return null;
        }

        transform.position = targetPosition;

        assignedBuilding = building;
        building.HasWorker = true;
        building.assignedWorker = this;

        targetBuilding = null;
        moveCoroutine = null;
    }
}