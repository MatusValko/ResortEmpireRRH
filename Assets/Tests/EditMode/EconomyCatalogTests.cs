using System;

using NUnit.Framework;
using ResortEmpire.Economy;
using ResortEmpire.Services;
using UnityEditor;
using UnityEngine;

namespace ResortEmpire.Tests
{
    [Category("Economy")]
    public sealed class EconomyCatalogTests
    {
        private EconomyCatalog _catalog;


        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<EconomyCatalog>();
        }


        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_catalog);
        }


        [Test]
        public void DefaultCatalogIsValid()
        {
            Assert.That(_catalog.TryValidate(out string error), Is.True, error);
        }


        [Test]
        public void DefaultCatalogPaysForEveryService()
        {
            foreach (ServiceType service in Enum.GetValues(typeof(ServiceType)))
                Assert.That(_catalog.GetReward(service), Is.GreaterThan(0), service.ToString());
        }


        [Test]
        public void MissingServiceIsInvalid()
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty("Rewards").arraySize = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }


        [Test]
        public void DuplicateServiceIsInvalid()
        {
            SerializedObject serialized = new(_catalog);
            SerializedProperty rewards = serialized.FindProperty("Rewards");
            rewards.GetArrayElementAtIndex(1).FindPropertyRelative("Service").enumValueIndex
                = rewards.GetArrayElementAtIndex(0).FindPropertyRelative("Service").enumValueIndex;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }


        [Test]
        public void NegativeRewardIsInvalid()
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty("Rewards").GetArrayElementAtIndex(0).FindPropertyRelative("Coins").intValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }


        [Test]
        public void NegativeStartingCoinsAreInvalid()
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty("StartingCoins").longValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }
    }
}
