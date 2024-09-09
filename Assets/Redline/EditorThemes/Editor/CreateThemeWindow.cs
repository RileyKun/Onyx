using System.IO;
using UnityEditor;
using UnityEngine;

//to do TextColor
//EditorStyles.label.normal.textColor 

namespace Redline.EditorThemes.Editor 
{
    public class CreateThemeWindow : EditorWindow
    {
        private enum UnityTheme { FullDark, FullLight, Dark, Light, Both }

        private UnityTheme _unityTheme;


        [MenuItem("Redline/Themes/Create Theme")]
        public static void ShowWindow()
        {
            ThemeSettings.ShowWindow();
            EditorWindow.GetWindow<CreateThemeWindow>("Theme Settings");




        }


        private string _name = "EnterName";
        private void OnGUI()
        {
            EditorGUILayout.LabelField("");


            _name = EditorGUILayout.TextField(_name, GUILayout.Width(200));

            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("Preset:");

            _unityTheme = (UnityTheme)EditorGUILayout.EnumPopup(_unityTheme, GUILayout.Width(100));
            var description = _unityTheme switch
            {
                UnityTheme.FullDark => "Everything you need for a Dark Theme",
                UnityTheme.FullLight => "Everything you need for a Light Theme",
                UnityTheme.Light => "Minimalistic Preset for a Light Theme",
                UnityTheme.Dark => "Minimalistic Preset for a Dark Theme",
                UnityTheme.Both => "Minimalistic Preset for a Light & Dark Theme",
                _ => ""
            };

            EditorGUILayout.LabelField(description);
            EditorGUILayout.LabelField("");

            var create = false;
            
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return)
                {
                    create = true;
                }
            }
            if(GUILayout.Button("Create Custom Theme", GUILayout.Width(200)))
            {
                create = true;
            }


            if (!create) return;
            var path = Application.dataPath + "/EditorThemes/Editor/StyleSheets/Extensions/CustomThemes/" + _name + ".json";
            if (File.Exists(path))
            {
                if( EditorUtility.DisplayDialog("This Theme already exsists", "Do you want to overide the old Theme?", "Yes",  "Cancel") == false)
                {
                    return;
                }
            }

            var presetName = _unityTheme switch
            {
                UnityTheme.FullDark => "FullDark",
                UnityTheme.FullLight => "FullLight",
                UnityTheme.Light => "Light",
                UnityTheme.Dark => "Dark",
                UnityTheme.Both => "Both",
                _ => ""
            };

            var t = FetchTheme(presetName,_name);



            ThemesUtility.SaveJsonFileForTheme(t);

            ThemesUtility.OpenEditTheme(t);

            this.Close();



        }

        private static CustomTheme FetchTheme(string presetName,string name)
        {
            var customTheme = ThemesUtility.GetCustomThemeFromJson(ThemesUtility.PresetsPath + presetName + ".json");

            customTheme.name = name;
            


            return customTheme;
        }

    }

}
