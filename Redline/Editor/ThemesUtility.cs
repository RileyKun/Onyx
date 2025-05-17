using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Redline.Editor
{ 
    public static class ThemesUtility
    {
        public const string CustomThemesPath = @"Packages\dev.redline-team.rpm\Redline\Editor\Themes\";
        private const string UssFilePath = @"Packages\dev.redline-team.rpm\Redline\Editor\StyleSheets\Extensions\";
        public const string PresetsPath = @"Packages\dev.redline-team.rpm\Redline\Editor\CreatePresets\";
        private const string Version = "v0.65";
        public const string Enc = ".json";

        public static string CurrentTheme;
        

        public static Color HtmlToRgb(string s)
        {
            ColorUtility.TryParseHtmlString(s, out var c);
            return c;
        }

        public static void OpenEditTheme(CustomTheme ct)
        {
            EditThemeWindow.Ct = ct;
            var window = (EditThemeWindow)EditorWindow.GetWindow(typeof(EditThemeWindow), false, "Edit Theme");
           
            window.Show();
        }
        public static CustomTheme GetCustomThemeFromJson(string Path)
        {
            var json = File.ReadAllText(Path);
            
            return JsonUtility.FromJson<CustomTheme>(json);
        }

        public static string GetPathForTheme(string Name)
        {
            return CustomThemesPath + Name + Enc;
        }
        public static void DeleteFileWithMeta(string Path)
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
                File.Delete(Path + ".meta");
            }
            else Debug.LogWarning("Path: " + Path + " does not exist");
            
        }

        private static string GenerateUssString(CustomTheme c)
        {
            var ussText = "";
            ussText += "/* ========== Editor Themes Plugin ==========*/";
            ussText += "\n";
            ussText += "/*            Auto Generated Code            */";
            ussText += "\n";
            ussText += "/*@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@*/";
            ussText += "\n";
            ussText += "/*"+ Version + "*/";

            return c.Items.Aggregate(ussText, (current, I) => current + UssBlock(I.Name, I.Color));
        }


        private static string UssBlock(string Name, Color Color)
        {
            Color32 color32 = Color;
            //Debug.Log(color32);
            var a = Color.a + "";
            a = a.Replace(",", ".");

            var Colors = "rgba(" + color32.r + ", " + color32.g + ", " + color32.b + ", " + a + ")";// Generate colors for later

            var s = "\n" + "\n";//add two empty lines

            s += "." + Name + "\n";//add name
            s += "{" + "\n" + "\t" + "background-color: " + Colors + ";" + "\n" + "}";//add color

            return s;
        }

        public static void SaveJsonFileForTheme(CustomTheme t)
        {

            t.Version = Version;
            var newJson = JsonUtility.ToJson(t);


            var Path = GetPathForTheme(t.Name);
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }

            File.WriteAllText(Path, newJson);
            LoadUssFileForTheme(t.Name);

        }
        public static void LoadUssFileForTheme(string Name)
        {
            LoadUssFileForThemeUsingPath(GetPathForTheme(Name));
        }

        private static void LoadUssFileForThemeUsingPath(string Path)
        {

            var t = GetCustomThemeFromJson(Path);

            if ((EditorGUIUtility.isProSkin && t.unityTheme == CustomTheme.UnityTheme.Light) || (!EditorGUIUtility.isProSkin && t.unityTheme == CustomTheme.UnityTheme.Dark))
            {
                InternalEditorUtility.SwitchSkinAndRepaintAllViews();

            }

            var ussText = GenerateUssString(t);
            WriteUss(ussText);

            CurrentTheme = Path;
        }


        private static void WriteUss(string ussText)
        {
            const string path = UssFilePath + "/dark.uss";
            DeleteFileWithMeta(path);

            File.WriteAllText(path, ussText);


            const string path2 = @"Packages\dev.redline-team.rpm\Redline\Editor\StyleSheets\Extensions\light.uss";
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
                case 2://secondery
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
                    colorList.Add("dragtab-label");//changing this color has overriden dragTab and dragtab first so removed
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
