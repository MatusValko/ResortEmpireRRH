using ResortEmpire.Buildings;
using UnityEngine;
using System.Collections; // Потрібно для IEnumerator

public class Worker : MonoBehaviour
{
    public ServiceBuildingBehaviour assignedBuilding;
    public float moveSpeed = 5f; // Швидкість переміщення
    
    private Coroutine moveCoroutine;

    public void MoveToPoint(Transform targetPoint)
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }
        
        moveCoroutine = StartCoroutine(SmoothMove(targetPoint.position));
    }

    private IEnumerator SmoothMove(Vector3 targetPosition)
    {
        while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            
            yield return null; 
        }
        
        transform.position = targetPosition; 
    }
}