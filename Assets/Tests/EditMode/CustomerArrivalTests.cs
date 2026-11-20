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
        public void EachRequestReleasesOneCustomerOfItsService_EvenBeforeDisembarking()
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
                presentationType.GetField("_customers", PrivateInstance).SetValue(presentation, host.transform);
                var send = presentationType.GetMethod("OnRequestCreated", PrivateInstance);
                var add = routeType.GetMethod("Add");
                var requested = typeof(CustomerWalk).GetField("_requested", PrivateInstance);
                var requestConstructor = typeof(RequestArrival).GetConstructor(PrivateInstance, null,
                    new[] { typeof(long), typeof(int), typeof(ServiceType), typeof(double) }, null);

                void Send(ServiceType service, long id)
                {
                    object request = requestConstructor.Invoke(new object[] { id, 1, service, 0d });
                    send.Invoke(presentation, new[] { request });
                }

                CustomerWalk AddCustomer(object route)
                {
                    var guest = new GameObject("Guest").AddComponent<CustomerWalk>();
                    guest.transform.SetParent(host.transform);
                    add.Invoke(route, new object[] { guest });
                    return guest;
                }

                Send(ServiceType.Drinks, 1);
                CustomerWalk firstDrinksGuest = AddCustomer(drinks);
                CustomerWalk secondDrinksGuest = AddCustomer(drinks);
                CustomerWalk foodGuest = AddCustomer(food);
                Assert.That(requested.GetValue(firstDrinksGuest), Is.True);
                Assert.That(requested.GetValue(secondDrinksGuest), Is.False);
                Assert.That(requested.GetValue(foodGuest), Is.False);

                Send(ServiceType.Food, 2);
                Assert.That(requested.GetValue(foodGuest), Is.True);
                Assert.That(requested.GetValue(secondDrinksGuest), Is.False);

                Send(ServiceType.Drinks, 3);
                Assert.That(requested.GetValue(secondDrinksGuest), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
