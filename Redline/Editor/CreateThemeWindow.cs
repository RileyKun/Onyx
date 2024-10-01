using System.IO;
using UnityEditor;
using UnityEngine;

//to do TextColor
//EditorStyles.label.normal.textColor 

namespace Redline.Editor 
{
    public class CreateThemeWindow : EditorWindow
    {
        enum UnityTheme { FullDark, FullLight, Dark, Light, Both }
        UnityTheme unityTheme;


        [MenuItem("Redline/Themes/Create Theme")]
        public static void ShowWindow()
        {
            ThemeSettings.ShowWindow();
            GetWindow<CreateThemeWindow>("Theme Settings");




        }



        string Name = "EnterName";
        private void OnGUI()
        {
            EditorGUILayout.LabelField("");


            Name = EditorGUILayout.TextField(Name, GUILayout.Width(200));

            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("Preset:");

            unityTheme = (UnityTheme)EditorGUILayout.EnumPopup(unityTheme, GUILayout.Width(100));
            var Description = unityTheme switch
            {
                UnityTheme.FullDark => "Everything you need for a Dark Theme",
                UnityTheme.FullLight => "Everything you need for a Light Theme",
                UnityTheme.Light => "Minimalistic Preset for a Light Theme",
                UnityTheme.Dark => "Minimalistic Preset for a Dark Theme",
                UnityTheme.Both => "Minimalistic Preset for a Light & Dark Theme",
                _ => ""
            };

            EditorGUILayout.LabelField(Description);
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
            var Path = @"Packages\dev.runaxr.redline\Redline\Editor\StyleSheets\Extensions\CustomThemes\" + Name + ".json";
            if (File.Exists(Path))
            {
                if( EditorUtility.DisplayDialog("This Theme already exists", "Do you want to override the old Theme?", "Yes",  "Cancel") == false)
                {
                    return;
                }
            }

            var t = new CustomTheme();
            var PresetName = unityTheme switch
            {
                UnityTheme.FullDark => "FullDark",
                UnityTheme.FullLight => "FullLight",
                UnityTheme.Light => "Light",
                UnityTheme.Dark => "Dark",
                UnityTheme.Both => "Both",
                _ => ""
            };

            t = FetchTheme(PresetName,Name);



            ThemesUtility.SaveJsonFileForTheme(t);

            ThemesUtility.OpenEditTheme(t);

            Close();



        }

        CustomTheme FetchTheme(string PresetName,string Name)
        {
            var CustomTheme = ThemesUtility.GetCustomThemeFromJson(ThemesUtility.PresetsPath + PresetName + ".json");

            CustomTheme.Name = Name;
            


            return CustomTheme;
        }

    }

}
