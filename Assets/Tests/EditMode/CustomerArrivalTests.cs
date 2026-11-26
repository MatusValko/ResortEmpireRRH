using System;
using System.Collections;
using System.Reflection;

using NUnit.Framework;
using ResortEmpire.Requests;
using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ResortEmpire.Tests
{
    public sealed class CustomerArrivalTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;


        [Test]
        public void RequestsAssignRoutesToReadyCustomersInOrder()
        {
            var host = new GameObject("Customer Arrivals");
            try
            {
                var presentation = host.AddComponent<CustomerArrivalBehaviour>();
                Type presentationType = typeof(CustomerArrivalBehaviour);
                Type routeType = presentationType.GetNestedType("Route", BindingFlags.NonPublic);
                object drinks = Activator.CreateInstance(routeType, nonPublic: true);
                object food = Activator.CreateInstance(routeType, nonPublic: true);
                var routes = (IDictionary)presentationType.GetField("_routes", PrivateInstance).GetValue(presentation);
                routes.Add(ServiceType.Drinks, drinks);
                routes.Add(ServiceType.Food, food);
                var drinksPath = new[] { new GameObject("Drinks entrance").transform };
                var foodPath = new[] { new GameObject("Food entrance").transform };
                drinksPath[0].SetParent(host.transform);
                foodPath[0].SetParent(host.transform);
                var waypoints = routeType.GetField("Waypoints", PrivateInstance);
                waypoints.SetValue(drinks, drinksPath);
                waypoints.SetValue(food, foodPath);
                var queueField = routeType.GetField("_queue", PrivateInstance);
                void SetQueue(object route, Transform[] slots)
                {
                    var queue = Activator.CreateInstance(queueField.FieldType, PrivateInstance, null,
                        new object[] { null, slots, slots[0] }, null);
                    queueField.SetValue(route, queue);
                }
                SetQueue(drinks, drinksPath);
                SetQueue(food, foodPath);
                presentationType.GetField("_customers", PrivateInstance).SetValue(presentation, host.transform);
                var send = presentationType.GetMethod("OnRequestCreated", PrivateInstance);
                var ready = presentationType.GetMethod("OnCustomerReady", PrivateInstance);
                var requestId = typeof(CustomerWalk).GetProperty("RequestId", PrivateInstance);
                var path = typeof(CustomerWalk).GetField("_path", PrivateInstance);
                var requestConstructor = typeof(RequestArrival).GetConstructor(PrivateInstance, null,
                    new[] { typeof(long), typeof(int), typeof(ServiceType), typeof(double) }, null);

                void Send(ServiceType service, long id)
                {
                    object request = requestConstructor.Invoke(new object[] { id, 1, service, 0d });
                    send.Invoke(presentation, new[] { request });
                }

                CustomerWalk CreateCustomer()
                {
                    var guest = new GameObject("Guest").AddComponent<CustomerWalk>();
                    guest.transform.SetParent(host.transform);
                    return guest;
                }

                var first = CreateCustomer();
                var second = CreateCustomer();
                var third = CreateCustomer();
                Send(ServiceType.Drinks, 1);
                Assert.That(requestId.GetValue(first), Is.EqualTo(0L));
                Assert.That(path.GetValue(first), Is.Null);
                ready.Invoke(presentation, new object[] { first });
                Assert.That(requestId.GetValue(first), Is.EqualTo(1L));
                Assert.That(path.GetValue(first), Is.SameAs(drinksPath));

                ready.Invoke(presentation, new object[] { second });
                ready.Invoke(presentation, new object[] { third });
                Assert.That(requestId.GetValue(second), Is.EqualTo(0L));
                Send(ServiceType.Food, 2);
                Assert.That(requestId.GetValue(second), Is.EqualTo(2L));
                Assert.That(path.GetValue(second), Is.SameAs(foodPath));
                Assert.That(requestId.GetValue(third), Is.EqualTo(0L));

                Send(ServiceType.Drinks, 3);
                Assert.That(requestId.GetValue(third), Is.EqualTo(3L));
                Assert.That(path.GetValue(third), Is.SameAs(drinksPath));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
