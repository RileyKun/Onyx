using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace EditorThemes.Editor
{ 
    public static class ThemesUtility
    {
        public static readonly string CustomThemesPath = Application.dataPath + "/EditorThemes/Editor/Themes/";
        public static readonly string UssFilePath = Application.dataPath + "/EditorThemes/Editor/StyleSheets/Extensions/";
        public static readonly string PresetsPath = Application.dataPath + "/EditorThemes/Editor/CreatePresets/";
        public static readonly string Version = "v0.65";
        public static readonly string Enc = ".json";

        public static string CurrentTheme;
        

        public static Color HtmlToRgb(string s)
        {
            var c = Color.black;
            ColorUtility.TryParseHtmlString(s, out c);
            return c;
        }

        public static void OpenEditTheme(CustomTheme ct)
        {
            EditThemeWindow.Ct = ct;
            var window = (EditThemeWindow)EditorWindow.GetWindow(typeof(EditThemeWindow), false, "Edit Theme");
           
            window.Show();
        }
        public static CustomTheme GetCustomThemeFromJson(string path)
        {
            var json = File.ReadAllText(path);
            
            return JsonUtility.FromJson<CustomTheme>(json);
        }

        public static string GetPathForTheme(string name)
        {
            return CustomThemesPath + name + Enc;
        }
        public static void DeleteFileWithMeta(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                File.Delete(path + ".meta");
            }
            else Debug.LogWarning("Path: " + path + " does not exist");
            
        }

        public static string GenerateUssString(CustomTheme c)
        {
            var ussText = "";
            ussText += "/* ========== Editor Themes Plugin ==========*/";
            ussText += "\n";
            ussText += "/*            Auto Generated Code            */";
            ussText += "\n";
            ussText += "/*@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@*/";
            ussText += "\n";
            ussText += "/*"+ Version + "*/";

            return c.items.Aggregate(ussText, (current, I) => current + UssBlock(I.Name, I.color));
        }

        

        public static string UssBlock(string name, Color color)
        {
            Color32 color32 = color;
            //Debug.Log(color32);
            var a = color.a + "";
            a = a.Replace(",", ".");

            var colors = "rgba(" + color32.r + ", " + color32.g + ", " + color32.b + ", " + a + ")";// Generate colors for later

            var s = "\n" + "\n";//add two empty lines

            s += "." + name + "\n";//add name
            s += "{" + "\n" + "\t" + "background-color: " + colors + ";" + "\n" + "}";//add color

            return s;
        }

        public static void SaveJsonFileForTheme(CustomTheme t)
        {
            var newJson = JsonUtility.ToJson(t);


            var path = GetPathForTheme(t.name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.WriteAllText(path, newJson);
            LoadUssFileForTheme(t.name);

        }
        public static void LoadUssFileForTheme(string name)
        {
            LoadUssFileForThemeUsingPath(GetPathForTheme(name));
        }
        public static void LoadUssFileForThemeUsingPath(string path)
        {

            var t = GetCustomThemeFromJson(path);

            if ((EditorGUIUtility.isProSkin && t.unityTheme == CustomTheme.UnityTheme.Light) || (!EditorGUIUtility.isProSkin && t.unityTheme == CustomTheme.UnityTheme.Dark))
            {
                InternalEditorUtility.SwitchSkinAndRepaintAllViews();

            }

            var ussText = GenerateUssString(t);
            WriteUss(ussText);

            CurrentTheme = path;
        }


        public static void WriteUss(string ussText)
        {
            var path = UssFilePath + "/dark.uss";
            DeleteFileWithMeta(path);

            File.WriteAllText(path, ussText);


            var path2 = Application.dataPath + "/EditorThemes/Editor/StyleSheets/Extensions/light.uss";
            DeleteFileWithMeta(path2);
            
            File.WriteAllText(path2, ussText);


            AssetDatabase.Refresh();

        }


        public static List<string> GetColorListByInt(int i)
        {
            var colorList = new List<string>();


            switch (i)
            {
                case 0://base
                    colorList.Add("TabWindowBackground");
                    colorList.Add("ScrollViewAlt");
                    colorList.Add("label");
                    colorList.Add("ProjectBrowserTopBarBg");
                    colorList.Add("ProjectBrowserBottomBarBg");
                    break;
                case 1://accent
                    colorList.Add("dockHeader");
                    colorList.Add("TV LineBold");

                    break;
                case 2://secondary
                    colorList.Add("ToolbarDropDownToogleRight");
                    colorList.Add("ToolbarPopupLeft");
                    colorList.Add("ToolbarPopup");
                    colorList.Add("toolbarbutton");
                    colorList.Add("PreToolbar");
                    colorList.Add("AppToolbar");
                    colorList.Add("GameViewBackground");
                    colorList.Add("CN EntryInfoSmall");
                    colorList.Add("Toolbar");
                    colorList.Add("toolbarbutton");
                    colorList.Add("toolbarbuttonRight");

                    colorList.Add("ProjectBrowserIconAreaBg");

                    //colorList.Add("dragTab");//this is the currently clicked tab  has to be a different color than the other tabs
                    break;
                case 3://Tab
                    //colorList.Add("dragtab first");
                    colorList.Add("dragtab-label");//changing this color has overwritten dragTab and dragtab first so removed
                    break;
                case 4://button

                    colorList.Add("AppCommandLeft");
                    colorList.Add("AppCommandMid");
                    colorList.Add("AppCommand");
                    colorList.Add("AppToolbarButtonLeft");
                    colorList.Add("AppToolbarButtonRight");
                    colorList.Add("DropDown");
                    break;
                case 5:
                    colorList.Add("SceneTopBarBg");
                    colorList.Add("MiniPopup");
                    colorList.Add("TV Selection");
                    colorList.Add("ExposablePopupMenu");
                    colorList.Add("minibutton");
                    colorList.Add(" ToolbarSearchTextField");
                    break;


            }
            return colorList;

        }


    }

}
