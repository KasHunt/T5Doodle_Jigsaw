using System;
using UnityEditor;
using UnityEngine;

namespace Code.Scripts
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ConditionalShowAttribute : PropertyAttribute
    {
        // Name of the boolean field that must be true for this option to be enabled
        public readonly string ConditionBool;

        public ConditionalShowAttribute(string conditionBool)
        {
            ConditionBool = conditionBool;
        }
    }
        
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