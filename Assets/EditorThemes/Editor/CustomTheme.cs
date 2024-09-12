using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace EditorThemes.Editor
{   
    [System.Serializable]
    public class CustomTheme
    {
    
    
    
        [FormerlySerializedAs("Name")] public string name;
        
    
        public enum UnityTheme { Dark,Light,Both}
        public UnityTheme unityTheme;
        [FormerlySerializedAs("IsUnDeletable")] public bool isUnDeletable;
        [FormerlySerializedAs("IsUnEditable")] public bool isUnEditable;

        [FormerlySerializedAs("Items")] public List<UIItem> items;
        
        [System.Serializable]
        public class UIItem
        {
            public string Name;
            [FormerlySerializedAs("Color")] public Color color;
        
        }
    }
}


