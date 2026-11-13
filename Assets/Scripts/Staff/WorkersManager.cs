using UnityEngine;
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
    
    void Start()
    {
        mode = ManagerMode.free;
        
        chosenWorker = null;
        UI.SetActive(false);

        workers = GameObject.FindGameObjectsWithTag("Worker");
        buildings = GameObject.FindGameObjectsWithTag("Building");

        System.Collections.Generic.List<ServiceBuilding> availableBuildings = new();

        foreach (GameObject buildingObject in buildings)
        {
            ServiceBuilding building = buildingObject.GetComponent<ServiceBuilding>();

            if (building != null && !building.HasWorker)
            {
                availableBuildings.Add(building);
            }
        }

        foreach (GameObject workerObject in workers)
        {
            Worker worker = workerObject.GetComponent<Worker>();

            if (worker == null || worker.assignedBuilding != null)
                continue;

            if (availableBuildings.Count == 0)
                break;

            int randomIndex = Random.Range(0, availableBuildings.Count);
            ServiceBuilding selectedBuilding = availableBuildings[randomIndex];

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
            }
        }
        else if (Mouse.current != null &&
                 Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 clickPosition = Mouse.current.position.ReadValue();
            CastRay(clickPosition);
        }
    }

    private void CastRay(Vector2 screenPosition)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
        {
            Debug.Log("Hit: " + hit.collider.name);
            GameObject obj = hit.collider.gameObject;

            if(obj.CompareTag("Worker")){
                ChooseWorker(obj.GetComponent<Worker>());
                Debug.Log("Worker chosen");
            }

            if(obj.CompareTag("Building") && mode == ManagerMode.worker && chosenWorker){
                TryAssignWorker(obj.GetComponent<ServiceBuilding>(), chosenWorker);
            }
        }
    }

    

    
    public void TryAssignWorker(ServiceBuilding _building, Worker _worker){
        if(_building.HasWorker == false && _worker != null){
            AssignWorker(_building, _worker);
        }
        else
        {
            Debug.Log("Building already has a worker assigned or no worker chosen.");
            CancelWorkerAssignment();
        }
    }
    
    public void AssignWorker(ServiceBuilding building, Worker worker){
        if(chosenWorker.assignedBuilding != null)
        {
            chosenWorker.assignedBuilding.ResetWorkerAssignment();
        }

        building.HasWorker = true;
        chosenWorker.assignedBuilding = building;
        building.assignedWorker = chosenWorker;
        chosenWorker.MoveToPoint(building.workerPoint);
        
        CancelWorkerAssignment();
    }

    public void ChooseWorker(Worker worker){

        chosenWorker = worker;
        mode = ManagerMode.worker;
        HighlightAvailableBuildings(true);
        UI.SetActive(true);

    }

    public void CancelWorkerAssignment(){
        chosenWorker = null;
        mode = ManagerMode.free;
        CancelHighlightBuildings();
        UI.SetActive(false);
    }

    
   public void HighlightAvailableBuildings(bool highlight)
    {
        foreach (GameObject buildingObject in buildings)
        {
            ServiceBuilding building = buildingObject.GetComponent<ServiceBuilding>();

            if (building == null || building.HasWorker)
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
        foreach (GameObject buildingObject in buildings)
        {
            ServiceBuilding building = buildingObject.GetComponent<ServiceBuilding>();

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
