using UnityEngine;

public class ServiceBuilding : MonoBehaviour
{
    //Placeholder for the ServiceBuilding class. It will be used to assign workers to buildings.
    
    public bool HasWorker;

    public float HighlightIntensity = 1.2f;

    public Worker assignedWorker;

    public Transform workerPoint;

    public void ResetWorkerAssignment()
    {
        HasWorker = false;
        assignedWorker = null;
    }

}