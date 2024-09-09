using UnityEditor;
using UnityEngine;
using static UnityEngine.Color;

namespace Redline.Scripts.Editor {
    [InitializeOnLoad]
    public class RedlineInfo: EditorWindow {
      private
      const string Url = "https://github.com/Redline/Redline/";
      private
      const string Url1 = "https://trigon.systems/";
      private
      const string Link = "";
      private
      const string Link1 = "https://status.trigon.systems";

      static RedlineInfo() {
        EditorApplication.update -= DoSplashScreen;
        EditorApplication.update += DoSplashScreen;
      }

      private static void DoSplashScreen() {
        EditorApplication.update -= DoSplashScreen;
        if (!EditorPrefs.HasKey("Redline_ShowInfoPanel")) {
          EditorPrefs.SetBool("Redline_ShowInfoPanel", true);
        }

        if (EditorPrefs.GetBool("Redline_ShowInfoPanel"))
        {
        }
      }

      private static Vector2 _changeLogScroll;
      private static GUIStyle _toolkitHeader;
      private static GUIStyle _redlineBottomHeader;
      private static GUIStyle _redlineHeaderLearnMoreButton;
      private static GUIStyle _redlineBottomHeaderLearnMoreButton;

      [MenuItem("Redline/Info", false, 500)]
      private static void Init()
      {
        var window=(RedlineInfo)EditorWindow.GetWindow(typeof(RedlineInfo));
        window.Show();
      }
      

      public void OnEnable() {
        titleContent = new GUIContent("Redline Info");

        minSize = new Vector2(400, 720);;
        _redlineBottomHeader = new GUIStyle();
        _toolkitHeader = new GUIStyle {
          normal = {
              background = Resources.Load("RedlinePMHeader") as Texture2D,
              textColor = white
            },
            fixedHeight = 200
        };
      }

      public void OnGUI() {
        GUILayout.Box("", _toolkitHeader);
        _redlineHeaderLearnMoreButton = EditorStyles.miniButton;
        _redlineHeaderLearnMoreButton.normal.textColor = black;
        _redlineHeaderLearnMoreButton.fontSize = 12;
        _redlineHeaderLearnMoreButton.border = new RectOffset(10, 10, 10, 10);
        var texture = AssetDatabase.GetBuiltinExtraResource < Texture2D > ("UI/Skin/UISprite.psd");
        _redlineHeaderLearnMoreButton.normal.background = texture;
        _redlineHeaderLearnMoreButton.active.background = texture;
        GUILayout.Space(4);
        GUI.backgroundColor = new Color(
          EditorPrefs.GetFloat("RedlineColor_R"),
          EditorPrefs.GetFloat("RedlineColor_G"),
          EditorPrefs.GetFloat("RedlineColor_B"),
          EditorPrefs.GetFloat("RedlineColor_A")
        );
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("The Black Arms Ultimate Development Kit")) {
          Application.OpenURL(Url);
        }

        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Trigon.Systems")) {
          Application.OpenURL(Url1 + Link);
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        //Update assets
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Status")) {
          Application.OpenURL(Link1);
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.Label("Redline Version 1.0.1");
        GUILayout.Space(2);
        GUILayout.Label("Redline imported correctly if you are seeing this");
        _changeLogScroll = GUILayout.BeginScrollView(_changeLogScroll, GUILayout.Width(390));

        GUILayout.Label(
          @" 
== Redline Package Manager ==

This Unity Kit is hopefully providing everything you need
I shot the update prompt for annoying up to date user
DOCKABLE WINDOWS ARE HERE!

---------------------------------------------------------
∞∞∞∞∞∞∞∞∞∞∞∞Information∞∞∞∞∞∞∞∞∞∞∞∞
This unity kit provides tools and scripts for you
I am not responsible for misuse of these tools and scripts
The goal is to become the main package anyone needs
If you have issues visit the github repository issues tab
Updates can be done from within unity itself (or manually)
Bugs/Issues can be reported via github issues
You can join our discord now :D
https://rpm.c0dera.in/discord

---------------------------------------------------------
∞∞∞∞Contributors to Redline Unity Kit∞∞∞∞
> Developer: PhoenixAceVFX
- Contributor : WTFBlaze -Made the import system
============================================
          ");
          GUILayout.EndScrollView(); GUILayout.Space(4);

          GUILayout.Box("", _redlineBottomHeader); _redlineBottomHeaderLearnMoreButton = EditorStyles.miniButton; _redlineBottomHeaderLearnMoreButton.normal.textColor = black; _redlineBottomHeaderLearnMoreButton.fontSize = 10; _redlineBottomHeaderLearnMoreButton.border = new RectOffset(10, 10, 10, 10); _redlineBottomHeaderLearnMoreButton.normal.background = texture; _redlineBottomHeaderLearnMoreButton.active.background = texture;

          GUILayout.FlexibleSpace(); GUILayout.BeginHorizontal(); EditorPrefs.SetBool("Redline_ShowInfoPanel",
            GUILayout.Toggle(EditorPrefs.GetBool("Redline_ShowInfoPanel"), "Show at startup")); GUILayout.EndHorizontal();
        }
      }
    }