using System.Collections.Generic;

using ResortEmpire.Buildings;
using ResortEmpire.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class WorkersManagers : MonoBehaviour
{

    public enum ManagerMode{worker,free}
    
    public Worker chosenWorker;
    public ManagerMode mode;

    private bool isActive = true;

    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask interactableLayer;

    [Header("UI ELEMENTS")]
    public GameObject UI;

    [Header("Buildings Highlighting")]
     public string shaderPropertyName = "OutlineSize";

     private GameObject[] buildings;
     private GameObject[] workers;

    private readonly List<RaycastResult> _uiRaycastResults = new();
    
    void Start()
    {
        mode = ManagerMode.free;
        
        chosenWorker = null;
        if (UI != null)
            UI.SetActive(false);

        workers = GameObject.FindGameObjectsWithTag("Worker");
        buildings = GameObject.FindGameObjectsWithTag("Building");

        System.Collections.Generic.List<ServiceBuildingBehaviour> availableBuildings = new();

        foreach (GameObject buildingObject in buildings)
        {
            ServiceBuildingBehaviour building = buildingObject != null ? buildingObject.GetComponent<ServiceBuildingBehaviour>() : null;

            if (CanAssignWorker(building))
            {
                availableBuildings.Add(building);
            }
        }

        foreach (GameObject workerObject in workers)
        {
            Worker worker = workerObject.GetComponent<Worker>();

            if (worker == null || !worker.isActiveAndEnabled || worker.assignedBuilding != null)
                continue;

            if (availableBuildings.Count == 0)
                break;

            int randomIndex = Random.Range(0, availableBuildings.Count);
            ServiceBuildingBehaviour selectedBuilding = availableBuildings[randomIndex];

            selectedBuilding.HasWorker = true;
            selectedBuilding.assignedWorker = worker;
            worker.assignedBuilding = selectedBuilding;

            worker.transform.position = selectedBuilding.workerPoint.position;

            availableBuildings.RemoveAt(randomIndex);

            CancelHighlightBuildings();
        }
    }

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }


    //Point and click system to assign workers to buildings. How it works:
    //1. Click on a worker to choose it. The UI will appear.
    //2. Click on a building to assign the worker to it. The UI will disappear  
    //optional: click on the UI to cancel the assignment.
    private void Update()
    {
        if(!isActive) return;
        
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;

            if (touch.press.wasPressedThisFrame)
            {
                Vector2 tapPosition = touch.position.ReadValue();
                CastRay(tapPosition);

                return;
            }
        }
        if (Mouse.current != null &&
                 Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 clickPosition = Mouse.current.position.ReadValue();
            CastRay(clickPosition);
        }
    }

    private void CastRay(Vector2 screenPosition)
    {
        if (mainCamera == null || !mainCamera.isActiveAndEnabled || !mainCamera.pixelRect.Contains(screenPosition))
            return;

        if (EventSystem.current != null)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = screenPosition };
            _uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(pointer, _uiRaycastResults);

            foreach (RaycastResult result in _uiRaycastResults)
            {
                if (result.module is UnityEngine.UI.GraphicRaycaster)
                    return;
            }
        }

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
        {
            Worker worker = hit.collider.GetComponentInParent<Worker>();
            if (worker != null)
            {
                ChooseWorker(worker);

                return;
            }

            ServiceBuildingBehaviour building = hit.collider.GetComponentInParent<ServiceBuildingBehaviour>();
            if (building != null && mode == ManagerMode.worker && chosenWorker != null)
                TryAssignWorker(building, chosenWorker);
        }
    }

    

    
    public void TryAssignWorker(ServiceBuildingBehaviour building, Worker worker)
    {
        AssignWorker(building, worker);
    }


    public void AssignWorker(ServiceBuildingBehaviour building, Worker worker)
    {
        if (worker == null || !worker.isActiveAndEnabled || !CanAssignWorker(building))
        {
            Debug.Log("Worker or target building is unavailable.");
            CancelWorkerAssignment();

            return;
        }

        if (worker.assignedBuilding != null)
            worker.assignedBuilding.ResetWorkerAssignment();

        building.HasWorker = true;
        worker.assignedBuilding = building;
        building.assignedWorker = worker;
        worker.MoveToPoint(building.workerPoint);
        
        CancelWorkerAssignment();
    }

    public void ChooseWorker(Worker worker){
        if (worker == null || !worker.isActiveAndEnabled)
        {
            CancelWorkerAssignment();

            return;
        }

        chosenWorker = worker;
        mode = ManagerMode.worker;
        HighlightAvailableBuildings(true);
        if (UI != null)
            UI.SetActive(true);

    }

    public void CancelWorkerAssignment(){
        chosenWorker = null;
        mode = ManagerMode.free;
        CancelHighlightBuildings();
        if (UI != null)
            UI.SetActive(false);
    }


    private static bool CanAssignWorker(ServiceBuildingBehaviour building)
    {
        if (building == null || !building.isActiveAndEnabled || building.workerPoint == null
            || building.HasWorker || building.assignedWorker != null)
            return false;

        ServiceDestination destination = building.GetComponentInParent<ServiceDestination>();

        return destination != null && destination.IsAvailable;
    }

    
   public void HighlightAvailableBuildings(bool highlight)
    {
        if (buildings == null)
            return;

        foreach (GameObject buildingObject in buildings)
        {
            ServiceBuildingBehaviour building = buildingObject != null ? buildingObject.GetComponent<ServiceBuildingBehaviour>() : null;

            if (!CanAssignWorker(building))
                continue;

            Renderer[] renderers = buildingObject.GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.materials)
                {
                    bool hasProperty = material.HasProperty(shaderPropertyName);

                    Debug.Log($"{material.name} | {shaderPropertyName} | HasProperty: {hasProperty}");

                    if (hasProperty)
                    {
                        float intensity = highlight ? building.HighlightIntensity : 0f;
                        material.SetFloat(shaderPropertyName, intensity);

                        Debug.Log($"Set {shaderPropertyName} to {intensity}");
                    }
                }
            }
        }
    }

    public void CancelHighlightBuildings()
    {
        if (buildings == null)
            return;

        foreach (GameObject buildingObject in buildings)
        {
            ServiceBuildingBehaviour building = buildingObject != null ? buildingObject.GetComponent<ServiceBuildingBehaviour>() : null;

            if (building == null)
                continue;

            Renderer[] renderers = buildingObject.GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.materials)
                {
                    bool hasProperty = material.HasProperty(shaderPropertyName);

                    if (hasProperty)
                    {
                        material.SetFloat(shaderPropertyName, 0f);
                    }
                }
            }
        }
    }



}
