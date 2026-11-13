using System;

using ResortEmpire.Services;
using UnityEngine;

namespace ResortEmpire.Buildings
{
    [CreateAssetMenu(fileName = "BuildingCatalog", menuName = "Resort Empire/Building Catalog")]
    public sealed class BuildingCatalog : ScriptableObject
    {
        [SerializeField] private BuildingDefinition[] Buildings = Array.Empty<BuildingDefinition>();


        public const int RequiredBuildingCount = 5;


        public BuildingDefinition GetBuilding(ServiceType service)
        {
            if (!TryValidate(out string error))
                throw new InvalidOperationException(error);

            for (int i = 0; i < Buildings.Length; i++)
            {
                if (Buildings[i].ServedService == service)
                    return Buildings[i];
            }

            throw new ArgumentOutOfRangeException(nameof(service), service, "No building serves this service type.");
        }


        public bool TryValidate(out string error)
        {
            if (Buildings == null || Buildings.Length != RequiredBuildingCount)
            {
                error = $"The catalog must contain exactly {RequiredBuildingCount} buildings.";

                return false;
            }

            for (int i = 0; i < Buildings.Length; i++)
            {
                if (Buildings[i] == null)
                {
                    error = $"Building {i + 1} is missing.";

                    return false;
                }

                if (!Buildings[i].TryValidate(out error))
                {
                    error = $"Building {i + 1}: {error}";

                    return false;
                }

                for (int j = 0; j < i; j++)
                {
                    if (Buildings[j].ServedService == Buildings[i].ServedService)
                    {
                        error = $"Buildings {j + 1} and {i + 1} serve the same service type.";

                        return false;
                    }
                }
            }

            error = null;

            return true;
        }
    }
}
