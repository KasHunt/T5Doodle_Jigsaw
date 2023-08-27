using System;
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
}