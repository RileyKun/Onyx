using System;
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
        
        public static CustomTheme Ct;

        string Name;

        private Vector2 _scrollPosition;


        private List<Color> _simpleColors = new();
        private List<Color> _lastSimpleColors = new();

        private enum CustomView { Simple, Advanced };

        private CustomView _customView;

        private bool _rhold;
        private bool _strgHold;


        
        
        private void OnDestroy()
        {
            Ct = null;

        }

        private void Awake()
        {
            //Debug.Log(ct.Items[0].Color);
            _simpleColors = CreateAverageCoolors();
            _lastSimpleColors = CreateAverageCoolors();


            Name = Ct.Name;
        }
        private void OnGUI()
        {
            

            
            if (Ct == null)
            {
                Close();
                return;
            }
                
                
            var regenerate = false;

            var e = Event.current;
            switch (e.type)
            {
                case EventType.KeyDown:
                {
                    if (e.keyCode == KeyCode.R)
                    {
                        _rhold = true;
                    }

                    break;
                }
                case EventType.KeyUp:
                {
                    if (e.keyCode == KeyCode.R)
                    {
                        _rhold = false;
                    }

                    break;
                }
                case EventType.MouseDown:
                case EventType.MouseUp:
                case EventType.MouseMove:
                case EventType.MouseDrag:
                case EventType.ScrollWheel:
                case EventType.Repaint:
                case EventType.Layout:
                case EventType.DragUpdated:
                case EventType.DragPerform:
                case EventType.DragExited:
                case EventType.Ignore:
                case EventType.Used:
                case EventType.ValidateCommand:
                case EventType.ExecuteCommand:
                case EventType.ContextClick:
                case EventType.MouseEnterWindow:
                case EventType.MouseLeaveWindow:
                case EventType.TouchDown:
                case EventType.TouchUp:
                case EventType.TouchMove:
                case EventType.TouchEnter:
                case EventType.TouchLeave:
                case EventType.TouchStationary:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            switch (e.type)
            {
                case EventType.KeyDown:
                {
                    if (e.keyCode == KeyCode.LeftControl)
                    {
                        _strgHold = true;
                    }

                    break;
                }
                case EventType.KeyUp:
                {
                    if (e.keyCode == KeyCode.LeftControl)
                    {
                        _strgHold = false;
                    }

                    break;
                }
                case EventType.MouseDown:
                case EventType.MouseUp:
                case EventType.MouseMove:
                case EventType.MouseDrag:
                case EventType.ScrollWheel:
                case EventType.Repaint:
                case EventType.Layout:
                case EventType.DragUpdated:
                case EventType.DragPerform:
                case EventType.DragExited:
                case EventType.Ignore:
                case EventType.Used:
                case EventType.ValidateCommand:
                case EventType.ExecuteCommand:
                case EventType.ContextClick:
                case EventType.MouseEnterWindow:
                case EventType.MouseLeaveWindow:
                case EventType.TouchDown:
                case EventType.TouchUp:
                case EventType.TouchMove:
                case EventType.TouchEnter:
                case EventType.TouchLeave:
                case EventType.TouchStationary:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (_rhold && _strgHold)
            {
                regenerate = true;
                _rhold = false;
                _strgHold = false;
            }

            if (regenerate && EditorUtility.DisplayDialog("Do you want to regenerate this Theme? (Make a Clone first!)", "Regenerating is helpful when the Theme was made with an older version of the Plugin (but you might loose small amounts of data)", "Continue", "Cancel") == true)
            {
                Ct.Items = new List<CustomTheme.UIItem>();
                //fetch all ColorObjects
                for (var i = 0; i < 6; i++)
                {
                    foreach (var uiItem in ThemesUtility.GetColorListByInt(i).Select(s => new CustomTheme.UIItem
                             {
                                 Name = s,
                                 Color = _simpleColors[i]
                             }))
                    {
                        Ct.Items.Add(uiItem);
                    }
                }
            }


            EditorGUILayout.LabelField("\n");

            Name = EditorGUILayout.TextField(Name);
            EditorGUILayout.LabelField("\n");
            _customView = (CustomView)EditorGUILayout.EnumPopup(_customView, GUILayout.Width(100));






            if (_customView == CustomView.Advanced)
            {
                EditorGUILayout.LabelField("");
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                var ctItemsClone = new List<CustomTheme.UIItem>(Ct.Items);
                foreach (var I in ctItemsClone)
                {
                    EditorGUILayout.BeginHorizontal();
                    I.Name = EditorGUILayout.TextField(I.Name, GUILayout.Width(200));
                    if (GUILayout.Button("Del", GUILayout.Width(50)))
                    {
                        Ct.Items.Remove(I);
                    }
                    EditorGUILayout.EndHorizontal();
                    I.Color = EditorGUILayout.ColorField(I.Color, GUILayout.Width(200));


                }
                EditorGUILayout.EndScrollView();


                EditorGUILayout.LabelField("");

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add", GUILayout.Width(200)))
                {
                    var I = new CustomTheme.UIItem
                    {
                        Name = "Enter Name"
                    };

                    Ct.Items.Add(I);
                }
                if (Ct.Items.Count > 0)
                {
                    if (GUILayout.Button("Remove", GUILayout.Width(200)))
                    {
                        Ct.Items.RemoveAt(Ct.Items.Count - 1);
                    }
                }

                EditorGUILayout.EndHorizontal();



            }
            else
            {
                GUILayout.Label("Base Color:", EditorStyles.boldLabel);
                _simpleColors[0] = EditorGUILayout.ColorField(_simpleColors[0]);
                GUILayout.Label("Accent Color:", EditorStyles.boldLabel);
                _simpleColors[1] = EditorGUILayout.ColorField(_simpleColors[1]);
                GUILayout.Label("Secondary Base Color:", EditorStyles.boldLabel);
                _simpleColors[2] = EditorGUILayout.ColorField(_simpleColors[2]);
                GUILayout.Label("Tab Color:", EditorStyles.boldLabel);
                _simpleColors[3] = EditorGUILayout.ColorField(_simpleColors[3]);
                GUILayout.Label("Command Bar Color:", EditorStyles.boldLabel);
                _simpleColors[4] = EditorGUILayout.ColorField(_simpleColors[4]);
                GUILayout.Label("Additional Color:", EditorStyles.boldLabel);
                _simpleColors[5] = EditorGUILayout.ColorField(_simpleColors[5]);






                for (var i = 0; i < _simpleColors.Count; i++)
                {
                    if (_simpleColors[i] != _lastSimpleColors[i])
                    {
                        //Debug.Log("not same");
                        EditColor(i, _simpleColors[i]);
                    }
                }

            }
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("Unity Theme:");
            Ct.unityTheme = (CustomTheme.UnityTheme)EditorGUILayout.EnumPopup(Ct.unityTheme, GUILayout.Width(100));
            EditorGUILayout.LabelField("");
            EditorGUILayout.BeginHorizontal();
            //Debug.Log(ct.Name);
            //Debug.Log(Name);
            if (GUILayout.Button("Save", GUILayout.Width(200)))
            {

                if (Ct.Name != Name)
                {
                    ThemesUtility.DeleteFileWithMeta(ThemesUtility.GetPathForTheme(Ct.Name));
                }

                Ct.Name = Name;

                ThemesUtility.SaveJsonFileForTheme(Ct);

            }

            if (!GUILayout.Button("Clone", GUILayout.Width(200))) return;
            Ct.Name = Name + " - c";

            ThemesUtility.SaveJsonFileForTheme(Ct);


        }


        private static CustomTheme.UIItem GeItemByName(string s)
        {
            CustomTheme.UIItem item = null;

            foreach (var u in Ct.Items.Where(u => u.Name == s))
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

                colors.Add(AllColors.Count > 0 ? GetAverage(AllColors) : ThemesUtility.HtmlToRgb("#9A7B6E"));
            }


            return colors;
        }

        void EditColor(int i, Color nc)
        {


            //Color difference = oc - nc;
            var edit = ThemesUtility.GetColorListByInt(i);


            foreach (var Item in edit.Select(GeItemByName).Where(Item => Item != null))
            {
                Item.Color = nc;
            }

            _lastSimpleColors[i] = _simpleColors[i];
        }

        private static Color GetAverage(List<Color> cl)
        {

            float r = 0;
            float g = 0;
            float b = 0;

            var count = cl.Count;
            foreach (var c in cl)
            {
                
                r += c.r;
                g += c.g;
                b += c.b;
            }



            return new Color(r / count, g / count, b / count);
        }

        
    }
}
