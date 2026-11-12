using NUnit.Framework;
using ResortEmpire.Services;
using UnityEditor;
using UnityEngine;

namespace ResortEmpire.Tests
{
    [Category("RequestBindings")]
    public sealed class ServiceDestinationBindingsTests
    {
        [TestCase("Reception", ServiceType.Rooms)]
        [TestCase("Restaurant", ServiceType.Food)]
        [TestCase("BeachBar", ServiceType.Drinks)]
        public void BuildingPrefabProvidesItsRequiredService(string prefabName, ServiceType expectedService)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Buildings/{prefabName}.prefab");
            Assert.That(prefab, Is.Not.Null);

            ServiceDestination destination = prefab.GetComponent<ServiceDestination>();
            Assert.That(destination, Is.Not.Null, "The service belongs on the building prefab root.");
            Assert.That(destination.Service, Is.EqualTo(expectedService));
            Assert.That(prefab.GetComponentsInChildren<ServiceDestination>(true), Has.Length.EqualTo(1), "A building must have one unambiguous destination.");
        }
    }
}
