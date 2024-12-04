using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Redline.Editor
{ 
    public static class ThemesUtility
    {
        private const string ThemeFolderPath = @"Packages\dev.runaxr.redline\Redline\Editor\Themes\";
        private const string UssFilePath = @"Packages\dev.runaxr.redline\Redline\Editor\StyleSheets\Extensions\";
        private const string Version = "v0.65";
        public const string Enc = ".json";

        public static string CurrentTheme;

        public static Color HtmlToRgb(string colorString)
        {
            ColorUtility.TryParseHtmlString(colorString, out var color);
            return color;
        }

        public static void OpenEditTheme(CustomTheme theme)
        {
            EditThemeWindow.Ct = theme;
            var window = (EditThemeWindow)EditorWindow.GetWindow(typeof(EditThemeWindow), false, "Edit Theme");
            window.Show();
        }

        public static CustomTheme GetCustomThemeFromJson(string path)
        {
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<CustomTheme>(json);
        }

        public static string GetPathForTheme(string themeName)
        {
            return Path.Combine(ThemeFolderPath, themeName + Enc);
        }

        public static void DeleteFileWithMeta(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                File.Delete(path + ".meta");
            }
            else
            {
                Debug.LogWarning($"Path: {path} does not exist");
            }
        }

        private static string GenerateUssString(CustomTheme theme)
        {
            var ussText = new StringBuilder();
            ussText.AppendLine("/* ========== Editor Themes Plugin ==========*/")
                   .AppendLine("/*            Auto Generated Code            */")
                   .AppendLine("/*@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@*/")
                   .AppendLine($"/*{Version}*/");

            foreach (var item in theme.Items)
            {
                ussText.Append(UssBlock(item.Name, item.Color));
            }

            return ussText.ToString();
        }

        private static string UssBlock(string name, Color color)
        {
            var color32 = color;
            var alpha = color.a.ToString("F2"); // format the alpha to 2 decimal places
            var rgbaColor = $"rgba({color32.r}, {color32.g}, {color32.b}, {alpha})";

            return $"\n\n.{name}\n{{\n\tbackground-color: {rgbaColor};\n}}";
        }

        public static void SaveJsonFileForTheme(CustomTheme theme)
        {
            theme.Version = Version;
            var newJson = JsonUtility.ToJson(theme);

            var path = GetPathForTheme(theme.Name);
            if (File.Exists(path))
            {
                DeleteFileWithMeta(path);
            }

            File.WriteAllText(path, newJson);
            LoadUssFileForTheme(theme.Name);
        }

        public static void LoadUssFileForTheme(string themeName)
        {
            var themePath = GetPathForTheme(themeName);
            LoadUssFileForThemeUsingPath(themePath);
        }

        private static void LoadUssFileForThemeUsingPath(string path)
        {
            var theme = GetCustomThemeFromJson(path);

            if (theme == null)
            {
                Debug.LogWarning($"Failed to load theme from {path}");
                return;
            }

            if ((EditorGUIUtility.isProSkin && theme.unityTheme == CustomTheme.UnityTheme.Light) ||
                (!EditorGUIUtility.isProSkin && theme.unityTheme == CustomTheme.UnityTheme.Dark))
            {
                InternalEditorUtility.SwitchSkinAndRepaintAllViews();
            }

            var ussText = GenerateUssString(theme);
            WriteUss(ussText);

            CurrentTheme = path;
        }

        private static void WriteUss(string ussText)
        {
            var darkUssPath = Path.Combine(UssFilePath, "dark.uss");
            var lightUssPath = Path.Combine(UssFilePath, "light.uss");

            DeleteFileWithMeta(darkUssPath);
            File.WriteAllText(darkUssPath, ussText);

            DeleteFileWithMeta(lightUssPath);
            File.WriteAllText(lightUssPath, ussText);

            AssetDatabase.Refresh();
        }

        public static List<string> GetColorListByInt(int category)
        {
            var colorList = category switch
            {
                0 => new List<string> { "TabWindowBackground", "ScrollViewAlt", "label", "ProjectBrowserTopBarBg", "ProjectBrowserBottomBarBg" },
                1 => new List<string> { "dockHeader", "TV LineBold" },
                2 => new List<string> { "ToolbarDropDownToogleRight", "ToolbarPopupLeft", "ToolbarPopup", "toolbarbutton", "PreToolbar", "AppToolbar", "GameViewBackground", "CN EntryInfoSmall", "Toolbar", "toolbarbuttonRight", "ProjectBrowserIconAreaBg" },
                3 => new List<string> { "dragtab-label" },
                4 => new List<string> { "AppCommandLeft", "AppCommandMid", "AppCommand", "AppToolbarButtonLeft", "AppToolbarButtonRight", "DropDown" },
                5 => new List<string> { "SceneTopBarBg", "MiniPopup", "TV Selection", "ExposablePopupMenu", "minibutton", " ToolbarSearchTextField" },
                _ => new List<string>(),
            };

            return colorList;
        }
    }
}
