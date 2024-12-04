using System.Collections.Generic;
using UnityEngine;

namespace Redline.Editor
{
    [System.Serializable]
    public class CustomTheme
    {
        // The name of the theme
        public string Name;

        // Enum representing the theme type (Dark, Light, Both)
        public enum UnityTheme { Dark, Light, Both }
        
        // Current theme type
        public UnityTheme unityTheme;

        // Flags to indicate if the theme is protected from deletion or editing
        public bool IsUnDeletable;
        public bool IsUnEditable;

        // Version of the theme
        public string Version;

        // List of UI items that are part of the theme
        public List<UIItem> Items = new List<UIItem>();

        // UIItem class to hold individual UI element name and color
        [System.Serializable]
        public class UIItem
        {
            public string Name;    // The name of the UI element
            public Color Color;    // The color associated with the UI element
        }

        // Constructor to initialize the theme with a name and version
        public CustomTheme(string name, string version, UnityTheme themeType)
        {
            Name = name;
            Version = version;
            unityTheme = themeType;
            IsUnDeletable = false;
            IsUnEditable = false;
            Items = new List<UIItem>();  // Initializes the list of UIItems
        }

        // Method to add a UI item to the theme
        public void AddUIItem(string itemName, Color color)
        {
            Items.Add(new UIItem { Name = itemName, Color = color });
        }

        // Method to update the color of an existing UI item
        public bool UpdateUIItemColor(string itemName, Color newColor)
        {
            var item = Items.Find(i => i.Name == itemName);
            if (item != null)
            {
                item.Color = newColor;
                return true; // Color updated successfully
            }
            return false; // Item not found
        }

        // Method to check if a UI item exists in the theme
        public bool ContainsUIItem(string itemName)
        {
            return Items.Exists(i => i.Name == itemName);
        }

        // Method to remove a UI item from the theme
        public bool RemoveUIItem(string itemName)
        {
            var item = Items.Find(i => i.Name == itemName);
            if (item != null)
            {
                Items.Remove(item);
                return true; // Item removed
            }
            return false; // Item not found
        }
    }
}
