using ResortEmpire.Waves;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ResortEmpire.Editor
{
    [CustomPropertyDrawer(typeof(WaveDefinition))]
    public sealed class WaveDefinitionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty wave = property.Copy();
            Foldout foldout = new() { value = property.isExpanded };
            foldout.RegisterValueChangedCallback(evt => wave.isExpanded = evt.newValue);
            foldout.Add(new PropertyField(wave.FindPropertyRelative("ArrivalDuration")));

            foreach (string field in new[] { "Rooms", "Food", "Drinks", "Relax", "OceanView" })
                foldout.Add(new PropertyField(wave.FindPropertyRelative(field)));

            UpdateLabel(foldout, wave);
            foldout.TrackPropertyValue(wave, _ => UpdateLabel(foldout, wave));

            return foldout;
        }


        private static void UpdateLabel(Foldout foldout, SerializedProperty wave)
        {
            long total = (long)wave.FindPropertyRelative("Rooms").intValue
                + wave.FindPropertyRelative("Food").intValue
                + wave.FindPropertyRelative("Drinks").intValue
                + wave.FindPropertyRelative("Relax").intValue
                + wave.FindPropertyRelative("OceanView").intValue;
            string path = wave.propertyPath;
            int bracket = path.LastIndexOf('[');
            string title = bracket >= 0 && int.TryParse(path.Substring(bracket + 1, path.Length - bracket - 2), out int index)
                ? $"Wave {index + 1}"
                : wave.displayName;
            foldout.text = $"{title} — {total} requests";
        }
    }
}
