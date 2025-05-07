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

// TODO: @Pixy; rewrite?
namespace RedlineUpdater.Editor{
  public class RedlineAutomaticUpdateAndInstall : MonoBehaviour{
    //get version from server
    private const string VersionURL = "https://c0dera.in/Redline/api/version.txt";

    //get download url
    private const string UnitypackageUrl = "https://c0dera.in/Redline/api/assets/latest/Redline.unitypackage";

    //GetVersion
    private static readonly string CurrentVersion = File.ReadAllText("Packages/dev.runaxr.redline/RedlineUpdater/Editor/RedlineVersion.txt");

    //EditorPrefs key for storing last declined version
    private const string LastDeclinedVersionKey = "Redline_LastDeclinedVersion";

    //Custom name for downloaded unitypackage
    private const string AssetName = "Redline.unitypackage";

    //gets Toolkit Directory Path
    private const string ToolkitPath = "Packages/dev.runaxr.redline";

    // ReSharper disable Unity.PerformanceAnalysis
    public static async void AutomaticRedlineInstaller(){
      //Starting Browser
      var httpClient = new HttpClient();
      //Reading Version data
      var result = await httpClient.GetAsync(VersionURL);
      var szServerVersion = await result.Content.ReadAsStringAsync();

      if(string.IsNullOrEmpty(szServerVersion))
        return;

      try{
        //Checking if Uptodate or not
        if(CurrentVersion == szServerVersion){
          RedlineLog("Alright we're up to date!"); //I finally shot the fucking prompt for annoying people, this is much better
          // Clear the last declined version since we're up to date
          EditorPrefs.DeleteKey(LastDeclinedVersionKey);
        }
        else{
          //not up to date
          RedlineLog("There is an Update Available");
          //start download
          await DownloadRedline();
        }
      }
      catch(Exception ex){
        Debug.LogError("[Redline] AssetDownloadManager:" + ex.Message);
      }
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private static async Task DownloadRedline(){
      // Check if this version was previously declined
      var lastDeclinedVersion = EditorPrefs.GetString(LastDeclinedVersionKey, string.Empty);
      if (lastDeclinedVersion == CurrentVersion) {
        RedlineLog("Update was previously declined for this version");
        return;
      }

      RedlineLog("Asking for Approval..");
      if(EditorUtility.DisplayDialog("Redline Updater",
           "Your version (V" + CurrentVersion + ") is outdated from the repo!" + " Do you wish to update?", "Yes", "No")){
        //starting deletion of old Redline
        await DeleteAndDownloadAsync();
      }
      else{
        //canceling the whole process
        RedlineLog("Update cancelled...");
        // Store the declined version
        EditorPrefs.SetString(LastDeclinedVersionKey, CurrentVersion);
      }
    }

    private static async Task DeleteAndDownloadAsync(){
      if(EditorUtility.DisplayDialog("Redline Updater",
           "Updater will now attempt to update the package manager. We would recommend backing up your project files in case something fails!",
           "OK")){
        try{
          // NOTE: Pixy; Scary! Verification of directories might be needed?
          var toolkitDir = Directory.GetFiles(ToolkitPath, "*.*");
          await Task.Run(() => {
            foreach(var f in toolkitDir){
              RedlineLog($"File {f} was deleted");
              File.Delete(f);
            }
          });
        }
        catch(DirectoryNotFoundException){
          RedlineLog("Update failed...");
          EditorUtility.DisplayDialog("Error Deleting Files",
            "Failed to update Redline! If this error persists, update Redline manually from the GitHub repository!",
            "OK");
        }
      }

      RedlineLog("Files deleted...");

      // refresh our assets database to reflect our new changes
      AssetDatabase.Refresh();

      // fetch the new files
      if(EditorUtility.DisplayDialog("Redline_Automatic_DownloadAndInstall", "Alright we're installing the new RPM now",
           "Nice!")){
        var w = new WebClient();
        w.Headers.Set(HttpRequestHeader.UserAgent, "Webkit Gecko wHTTPS (Keep Alive 55)");
        w.DownloadFileCompleted += FileDownloadComplete;
        w.DownloadProgressChanged += FileDownloadProgress;
        w.DownloadFileAsync(new Uri(UnitypackageUrl), AssetName);
      }
    }

    private static void FileDownloadProgress(object sender, DownloadProgressChangedEventArgs e){
      //Creates A ProgressBar
      var progress = e.ProgressPercentage;
      switch(progress){
        case< 0:
          return;
        case>= 100:
          EditorUtility.ClearProgressBar();
          break;
        default:
          EditorUtility.DisplayProgressBar("Download of " + AssetName,
            "Downloading " + AssetName + " " + progress + "%",
            (progress / 100F));
          break;
      }
    }

    private static void FileDownloadComplete(object sender, AsyncCompletedEventArgs e){
      //Checks if Download is complete
      if(e.Error == null){
        RedlineLog("Download completed!");
        //Opens .unitypackage
        Process.Start(AssetName);
      }
      else{
        //Asks to open Download Page Manually
        RedlineLog("Download failed!");
        if(EditorUtility.DisplayDialog("Redline_Automatic_DownloadAndInstall", "Something screwed up and we couldn't download the latest Redline",
             "Open URL instead", "Cancel")){
          Application.OpenURL(UnitypackageUrl);
        }
      }
    }

    private static void RedlineLog(string message){
      //Our Logger
      Debug.Log("[Redline] AssetDownloadManager: " + message);
    }
  }
}