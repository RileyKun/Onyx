using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

//to do TextColor
//EditorStyles.label.normal.textColor 

namespace Redline.Editor 
{ 

public class EditThemeWindow : EditorWindow
{
        
        public static CustomTheme ct;

        string Name;

        Vector2 scrollPosition;


        List<Color> SimpleColors = new List<Color>();
        List<Color> LastSimpleColors = new List<Color>();

        enum CustomView { Simple, Advanced };
        CustomView customView;

        bool Rhold;
        bool STRGHold;


        
        
        private void OnDestroy()
        {
            ct = null;

        }

        private void Awake()
        {
            //Debug.Log(ct.Items[0].Color);
            SimpleColors = CreateAverageCoolors();
            LastSimpleColors = CreateAverageCoolors();


            Name = ct.Name;
        }
        private void OnGUI()
        {
            

            
            if (ct == null)
            {
                Close();
                return;
            }
                
                
            var Regenerate = false;

            var e = Event.current;
            switch (e.type)
            {
                case EventType.KeyDown:
                {
                    if (e.keyCode == KeyCode.R)
                    {
                        Rhold = true;
                    }

                    break;
                }
                case EventType.KeyUp:
                {
                    if (e.keyCode == KeyCode.R)
                    {
                        Rhold = false;
                    }

                    break;
                }
            }

            switch (e.type)
            {
                case EventType.KeyDown:
                {
                    if (e.keyCode == KeyCode.LeftControl)
                    {
                        STRGHold = true;
                    }

                    break;
                }
                case EventType.KeyUp:
                {
                    if (e.keyCode == KeyCode.LeftControl)
                    {
                        STRGHold = false;
                    }

                    break;
                }
            }

            if (Rhold && STRGHold)
            {
                Regenerate = true;
                Rhold = false;
                STRGHold = false;
            }

            if (Regenerate && EditorUtility.DisplayDialog("Do you want to regenerate this Theme? (Make a Clone first!)", "Regenerating is helpful when the Theme was made with an older version of the Plugin (but you might loose small amounts of data)", "Continue", "Cancel") == true)
            {
                ct.Items = new List<CustomTheme.UIItem>();
                //fetch all ColorObjects
                for (var i = 0; i < 6; i++)
                {

                    foreach (var s in ThemesUtility.GetColorListByInt(i))
                    {
                        var uiItem = new CustomTheme.UIItem();
                        uiItem.Name = s;
                        uiItem.Color = SimpleColors[i];

                        ct.Items.Add(uiItem);
                    }
                }
            }


            EditorGUILayout.LabelField("\n");

            Name = EditorGUILayout.TextField(Name);
            EditorGUILayout.LabelField("\n");
            customView = (CustomView)EditorGUILayout.EnumPopup(customView, GUILayout.Width(100));






            if (customView == CustomView.Advanced)
            {
                EditorGUILayout.LabelField("");
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

                var CTItemsClone = new List<CustomTheme.UIItem>(ct.Items);
                foreach (var I in CTItemsClone)
                {
                    EditorGUILayout.BeginHorizontal();
                    I.Name = EditorGUILayout.TextField(I.Name, GUILayout.Width(200));
                    if (GUILayout.Button("Del", GUILayout.Width(50)))
                    {
                        ct.Items.Remove(I);
                    }
                    EditorGUILayout.EndHorizontal();
                    I.Color = EditorGUILayout.ColorField(I.Color, GUILayout.Width(200));


                }
                EditorGUILayout.EndScrollView();


                EditorGUILayout.LabelField("");

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add", GUILayout.Width(200)))
                {
                    var I = new CustomTheme.UIItem();
                    I.Name = "Enter Name";

                    ct.Items.Add(I);
                }
                if (ct.Items.Count > 0)
                {
                    if (GUILayout.Button("Remove", GUILayout.Width(200)))
                    {
                        ct.Items.RemoveAt(ct.Items.Count - 1);
                    }
                }

                EditorGUILayout.EndHorizontal();



            }
            else
            {
                GUILayout.Label("Base Color:", EditorStyles.boldLabel);
                SimpleColors[0] = EditorGUILayout.ColorField(SimpleColors[0]);
                GUILayout.Label("Accent Color:", EditorStyles.boldLabel);
                SimpleColors[1] = EditorGUILayout.ColorField(SimpleColors[1]);
                GUILayout.Label("Secondary Base Color:", EditorStyles.boldLabel);
                SimpleColors[2] = EditorGUILayout.ColorField(SimpleColors[2]);
                GUILayout.Label("Tab Color:", EditorStyles.boldLabel);
                SimpleColors[3] = EditorGUILayout.ColorField(SimpleColors[3]);
                GUILayout.Label("Command Bar Color:", EditorStyles.boldLabel);
                SimpleColors[4] = EditorGUILayout.ColorField(SimpleColors[4]);
                GUILayout.Label("Additional Color:", EditorStyles.boldLabel);
                SimpleColors[5] = EditorGUILayout.ColorField(SimpleColors[5]);






                for (var i = 0; i < SimpleColors.Count; i++)
                {
                    if (SimpleColors[i] != LastSimpleColors[i])
                    {
                        //Debug.Log("not same");
                        EditColor(i, SimpleColors[i]);
                    }
                }

            }
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("Unity Theme:");
            ct.unityTheme = (CustomTheme.UnityTheme)EditorGUILayout.EnumPopup(ct.unityTheme, GUILayout.Width(100));
            EditorGUILayout.LabelField("");
            EditorGUILayout.BeginHorizontal();
            //Debug.Log(ct.Name);
            //Debug.Log(Name);
            if (GUILayout.Button("Save", GUILayout.Width(200)))
            {

                if (ct.Name != Name)
                {
                    ThemesUtility.DeleteFileWithMeta(ThemesUtility.GetPathForTheme(ct.Name));
                }

                ct.Name = Name;

                ThemesUtility.SaveJsonFileForTheme(ct);

            }

            if (!GUILayout.Button("Clone", GUILayout.Width(200))) return;
            ct.Name = Name + " - c";

            ThemesUtility.SaveJsonFileForTheme(ct);


        }
        
        
        
        CustomTheme.UIItem GeItemByName(string s)
        {
            CustomTheme.UIItem item = null;

            foreach (var u in ct.Items.Where(u => u.Name == s))
            {
                item = u;
            }
            return item;
        }
        
        List<Color> CreateAverageCoolors()
        {
            var colors = new List<Color>();


            for (var i = 0; i < 6; i++)
            {
                var ColorObjects = ThemesUtility.GetColorListByInt(i);
                var AllColors = (from s in ColorObjects where GeItemByName(s) != null select GeItemByName(s).Color).ToList();

                if (AllColors.Count > 0)
                {
                    colors.Add(GetAverage(AllColors));
                }
                else
                {
                    colors.Add(ThemesUtility.HtmlToRgb("#9A7B6E"));
                }


            }


            return colors;
        }

        void EditColor(int i, Color nc)
        {


            //Color difrence = oc - nc;
            var edit = ThemesUtility.GetColorListByInt(i);


            foreach (var Item in edit.Select(s => GeItemByName(s)).Where(Item => Item != null))
            {
                Item.Color = nc;
            }

            LastSimpleColors[i] = SimpleColors[i];
        }

        Color GetAverage(List<Color> cl)
        {

            float r = 0;
            float g = 0;
            float b = 0;

            var Count = cl.Count;
            foreach (var c in cl)
            {
                
                r += c.r;
                g += c.g;
                b += c.b;
            }



            return new Color(r / Count, g / Count, b / Count);
        }

        
    }
}
