using ResortEmpire.Waves;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ResortEmpire.Editor
{
    [CustomEditor(typeof(WaveCatalog))]
    public sealed class WaveCatalogEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            HelpBox validation = new();
            root.Add(validation);
            UpdateValidation(validation);
            root.TrackSerializedObjectValue(serializedObject, _ => UpdateValidation(validation));

            return root;
        }


        private void UpdateValidation(HelpBox validation)
        {
            WaveCatalog catalog = (WaveCatalog)target;
            bool isValid = catalog.TryValidate(out string error);
            validation.text = error ?? string.Empty;
            validation.messageType = HelpBoxMessageType.Error;
            validation.style.display = isValid ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
