using System.Collections.Generic;
using ResortEmpire.Buildings;
using ResortEmpire.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class WorkersManagers : MonoBehaviour
{
    public enum ManagerMode { worker, free }

    public Worker chosenWorker;
    public ManagerMode mode;

    /// <summary>
    /// Fired when a worker is assigned or transferred to a building: (worker, previousBuilding, newBuilding).
    /// </summary>
    public event System.Action<Worker, ServiceBuildingBehaviour, ServiceBuildingBehaviour> WorkerAssigned;

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

        List<ServiceBuildingBehaviour> availableBuildings = new();

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

    private void Update()
    {
        if (!isActive) return;

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
                TryAssignWorker(GetAvailableBuilding(), worker);
                return;
            }

            ServiceBuildingBehaviour building = hit.collider.GetComponentInParent<ServiceBuildingBehaviour>();
            if (building != null && building.assignedWorker != null)
                TryAssignWorker(GetAvailableBuilding(), building.assignedWorker);
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

        ServiceBuildingBehaviour previousBuilding = worker.assignedBuilding != null
            ? worker.assignedBuilding
            : worker.targetBuilding;

        if (worker.assignedBuilding != null)
        {
            worker.assignedBuilding.ResetWorkerAssignment();
            worker.assignedBuilding = null;
        }

        worker.MoveToBuilding(building);

        WorkerAssigned?.Invoke(worker, previousBuilding, building);

        CancelWorkerAssignment();
    }

    public void ChooseWorker(Worker worker)
    {
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

    public void CancelWorkerAssignment()
    {
        chosenWorker = null;
        mode = ManagerMode.free;
        CancelHighlightBuildings();
        if (UI != null)
            UI.SetActive(false);
    }

    private bool CanAssignWorker(ServiceBuildingBehaviour building)
    {
        if (building == null || !building.isActiveAndEnabled || building.workerPoint == null
            || building.HasWorker || building.assignedWorker != null)
            return false;

        if (workers != null)
        {
            foreach (GameObject workerObject in workers)
            {
                if (workerObject == null) continue;
                Worker w = workerObject.GetComponent<Worker>();
                if (w != null && w.targetBuilding == building)
                    return false;
            }
        }

        ServiceDestination destination = building.GetComponentInParent<ServiceDestination>();

        return destination != null && destination.IsAvailable;
    }

    public ServiceBuildingBehaviour GetAvailableBuilding()
    {
        if (buildings == null)
            return null;

        foreach (GameObject buildingObject in buildings)
        {
            ServiceBuildingBehaviour building = buildingObject != null ? buildingObject.GetComponent<ServiceBuildingBehaviour>() : null;

            if (CanAssignWorker(building))
                return building;
        }

        return null;
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

                    if (hasProperty)
                    {
                        float intensity = highlight ? building.HighlightIntensity : 0f;
                        material.SetFloat(shaderPropertyName, intensity);
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
