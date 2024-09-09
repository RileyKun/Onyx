using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Redline.EditorThemes.Editor
{   
    [System.Serializable]
    public class CustomTheme
    {
    
    
    
        [FormerlySerializedAs("Name")] public string name;
        
    
        public enum UnityTheme { Dark,Light,Both}
        public UnityTheme unityTheme;
        [FormerlySerializedAs("IsUnDeletable")] public bool isUnDeletable;
        [FormerlySerializedAs("IsUnEditable")] public bool isUnEditable;
        [FormerlySerializedAs("Version")] public string version;
        
        [FormerlySerializedAs("Items")] public List<UIItem> items;
        
        [System.Serializable]
        public class UIItem
        {
            [FormerlySerializedAs("Name")] public string name;
            [FormerlySerializedAs("Color")] public Color color;
        
        }
    }
}


