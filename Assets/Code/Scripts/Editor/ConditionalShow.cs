using UnityEditor;
using UnityEngine;

namespace Code.Scripts.Editor
{
    [CustomPropertyDrawer(typeof(ConditionalShowAttribute))]
    public class ConditionalShowPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var conditionalShow = (ConditionalShowAttribute)attribute;
            var conditionProperty = property.serializedObject.FindProperty(conditionalShow.ConditionBool);
            
            var wasEnabled = GUI.enabled;
            GUI.enabled = conditionProperty.boolValue;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = wasEnabled;
        }
    }
}