using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace dev.runaxr.redline.RedlineUpdater.Editor {

  public class RedlineAutomaticUpdateAndInstall: MonoBehaviour {

    //get version from server
    private
    const string VersionURL = "https://c0dera.in/Redline/api/version.txt";

    //get download url
    private
    const string UnitypackageUrl = "https://c0dera.in/Redline/api/assets/latest/Redline.unitypackage"; //This fucker is case sensitive... LMAO it took me 3 updates to figure it out

    //GetVersion
    private static readonly string CurrentVersion = File.ReadAllText("Packages/dev.runaxr.Redline/RedlineUpdater/editor/RedlineVersion.txt");

    //select where to be imported (Redline)
    public static string AssetPath = "Assets\\"; //We put the unitypackage here temporarily
    //Custom name for downloaded unitypackage
    private
    const string AssetName = "Redline.unitypackage"; //We name it this because yes

    //gets Toolkit Directory Path
    private
    const string ToolkitPath = @"Packages\Redline\"; //This is the directory so the updater can kaboom the old files

    // ReSharper disable Unity.PerformanceAnalysis
    public static async void AutomaticRedlineInstaller() {
      //Starting Browser
      var httpClient = new HttpClient();
      //Reading Version data
      var result = await httpClient.GetAsync(VersionURL);
      var strServerVersion = await result.Content.ReadAsStringAsync();

      var thisVersion = CurrentVersion;

      try {
        //Checking if Uptodate or not
        if (strServerVersion == thisVersion) {
          RedlineLog("Alright we're up to date!"); //I finally shot the fucking prompt for annoying people, this is much better
        } else {
          //not up to date
          RedlineLog("There is an Update Available");
          //start download
          await DownloadRedline();
        }
      } catch (Exception ex) {
        Debug.LogError("[Redline] AssetDownloadManager:" + ex.Message);
      }
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private static async Task DownloadRedline() {
      RedlineLog("Asking for Approval..");
      if (EditorUtility.DisplayDialog("Redline Updater", "Your Version (V" + CurrentVersion.ToString() + ") is Outdated!" + " Do you wanna update?", "Yes", "No")) {
        //starting deletion of old Redline
        await DeleteAndDownloadAsync();
      } else {
        //canceling the whole process
        RedlineLog("Alright, I'll ask again later");
      }
    }

    private static async Task DeleteAndDownloadAsync() {
      try {
        if (EditorUtility.DisplayDialog("Redline_Automatic_DownloadAndInstall", "We're going to delete the old RPM real quick, This is so you dont have to manually do it in case of file structure changes :D", "Okay")) {
          //gets every file in Toolkit folder
          var toolkitDir = Directory.GetFiles(ToolkitPath, "*.*");

          try {
            //Deletes All Files in Toolkit folder, I moved the DiscordRPC to a separate package because unity would hit a fatal error trying to remove its dll
            await Task.Run(() => {
              foreach(var f in toolkitDir) {
                RedlineLog($"{f} - Deleted");
                File.Delete(f);
              }
            });
          } catch (Exception ex) {
            EditorUtility.DisplayDialog("Error Deleting Toolset", ex.Message, "Okay");
          } //haha, fuck you I don't need the catch anymore
        }
      } catch (DirectoryNotFoundException) {
        EditorUtility.DisplayDialog("Error Deleting Files", "Ah crap, We couldn't find the Redline Folder so you might have to do the update manually", "Ignore");
      }
      AssetDatabase.Refresh();

      if (EditorUtility.DisplayDialog("Redline_Automatic_DownloadAndInstall", "Alright we're installing the new RPM now", "Nice!")) {
        //Creates WebClient to Download latest .unitypackage
        var w = new WebClient();
        w.Headers.Set(HttpRequestHeader.UserAgent, "Webkit Gecko wHTTPS (Keep Alive 55)");
        w.DownloadFileCompleted += new AsyncCompletedEventHandler(FileDownloadComplete);
        w.DownloadProgressChanged += FileDownloadProgress;
        w.DownloadFileAsync(new Uri(UnitypackageUrl), AssetName);
      }
    }

    private static void FileDownloadProgress(object sender, DownloadProgressChangedEventArgs e) {
      //Creates A ProgressBar
      var progress = e.ProgressPercentage;
      switch (progress) {
      case < 0:
        return;
      case >= 100:
        EditorUtility.ClearProgressBar();
        break;
      default:
        EditorUtility.DisplayProgressBar("Download of " + AssetName,
          "Downloading " + AssetName + " " + progress + "%",
          (progress / 100F));
        break;
      }
    }

    private static void FileDownloadComplete(object sender, AsyncCompletedEventArgs e) {
      //Checks if Download is complete
      if (e.Error == null) {
        RedlineLog("Download completed!");
        //Opens .unitypackage
        Process.Start(AssetName);
      } else {
        //Asks to open Download Page Manually
        RedlineLog("Download failed!");
        if (EditorUtility.DisplayDialog("Redline_Automatic_DownloadAndInstall", "Something screwed up and we couldn't download the latest Redline", "Open URL instead", "Cancel")) {
          Application.OpenURL(UnitypackageUrl);
        }
      }
    }

    private static void RedlineLog(string message) {
      //Our Logger
      Debug.Log("[Redline] AssetDownloadManager: " + message);
    }
  }
}