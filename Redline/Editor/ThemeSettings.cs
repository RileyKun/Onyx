using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

//to do TextColor
//EditorStyles.label.normal.textColor 

namespace Redline.Editor
{

    public class ThemeSettings : EditorWindow
    {
        public List<string> AllThemes = new();
        

        [MenuItem("Redline/Themes/Select Themes")]
        public static void ShowWindow()
        {
            GetWindow<ThemeSettings>("Theme Settings");
        }


        

        public string ThemeName;

        private Vector2 _scrollPosition;


        private void OnGUI()
        {
            
            //window code

            GUILayout.Label("Create & Select Themes", EditorStyles.boldLabel);
            GUILayout.Label("Currently Selected: " + Path.GetFileNameWithoutExtension(ThemesUtility.CurrentTheme), EditorStyles.boldLabel);



            if (GUILayout.Button("Create new Theme"))
            {
                
                var window = (CreateThemeWindow)GetWindow(typeof(CreateThemeWindow), false, "Create Theme");
                window.Show();
            }
            GUILayout.Label("or Select:", EditorStyles.boldLabel);

            

            var darkThemes = new List<CustomTheme>();
            var lightThemes = new List<CustomTheme>();
            var bothThemes = new List<CustomTheme>();

            foreach (var s in Directory.GetFiles(ThemesUtility.CustomThemesPath, "*"+ ThemesUtility.Enc))
            {
                
                var ct = ThemesUtility.GetCustomThemeFromJson(s);
                switch (ct.unityTheme)
                {
                    case CustomTheme.UnityTheme.Dark:
                        darkThemes.Add(ct);
                        break;
                    case CustomTheme.UnityTheme.Light:
                        lightThemes.Add(ct);
                        break;
                    case CustomTheme.UnityTheme.Both:
                        bothThemes.Add(ct);
                        break ;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("Dark & Light Themes:");
            foreach (var ct in bothThemes)
            {
                DisplayGUIThemeItem(ct);
            }


            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("Dark Themes:");
            foreach(var ct in darkThemes)
            {
                DisplayGUIThemeItem(ct);
            }
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("Light Themes:");
            foreach (var ct in lightThemes)
            {
                DisplayGUIThemeItem(ct);
            }

            EditorGUILayout.EndScrollView();


        }


        private static void DisplayGUIThemeItem(CustomTheme ct)
        {
            
            var Name = ct.Name;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Name))
            {

                ThemesUtility.LoadUssFileForTheme(Name);
            }

            if (!ct.IsUnEditable && GUILayout.Button("Edit", GUILayout.Width(70)))
            {

                ThemesUtility.OpenEditTheme(ct);
                //EditThemeWindow.ct = ct;
                //EditThemeWindow window = (EditThemeWindow)EditorWindow.GetWindow(typeof(EditThemeWindow), false, "Edit Theme");

                //window.Show();

            }
            if (!ct.IsUnDeletable && GUILayout.Button("Delete", GUILayout.Width(70)))
            {
                if (EditorUtility.DisplayDialog("Do you want to Delete " + ct.Name + " ?", "Do you want to Permanently Delete the Theme " + ct.Name +" (No undo!)", "Delete", "Cancel") == false)
                {
                    return;
                }


                ThemesUtility.DeleteFileWithMeta(ThemesUtility.GetPathForTheme(Name));

                ThemesUtility.LoadUssFileForTheme("_default");

            }

            EditorGUILayout.EndHorizontal();
        }
    


    }
}