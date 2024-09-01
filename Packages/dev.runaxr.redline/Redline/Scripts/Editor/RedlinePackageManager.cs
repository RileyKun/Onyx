using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using UnityEditor;
using UnityEngine;

namespace dev.runaxr.redline.Redline.Scripts.Editor {
  public abstract class RedlineImportManager {
    private
    const string V = "https://c0dera.in/Redline/api/assets/";
    public static readonly string ConfigName = "importConfig.json";
    public static string ServerUrl = V;
    private static readonly string InternalServerUrl = V;

    public static void DownloadAndImportAssetFromServer(string assetName) {
      if (File.Exists(RedlineSettings.GetAssetPath() + assetName)) {
        RedlineLog(assetName + " exists. Importing it..");
        ImportDownloadedAsset(assetName);
      } else {
        RedlineLog(assetName + " does not exist. Starting download..");
        DownloadFile(assetName);
      }
    }

    private static void DownloadFile(string assetName) {
      var w = new WebClient();
      w.Headers.Set(HttpRequestHeader.UserAgent, "Webkit Gecko wHTTPS (Keep Alive 55)");
      w.QueryString.Add("assetName", assetName);
      w.DownloadFileCompleted += FileDownloadCompleted;
      w.DownloadProgressChanged += FileDownloadProgress;
      var url = ServerUrl + assetName;
      w.DownloadFileAsync(new Uri(url), RedlineSettings.GetAssetPath() + assetName);
    }

    public static void DeleteAsset(string assetName) {
      File.Delete(RedlineSettings.GetAssetPath() + assetName);
    }

    public static void UpdateConfig() {
      var w = new WebClient();
      w.Headers.Set(HttpRequestHeader.UserAgent, "Webkit Gecko wHTTPS (Keep Alive 55)");
      w.DownloadFileCompleted += ConfigDownloadCompleted;
      w.DownloadProgressChanged += FileDownloadProgress;
      var url = InternalServerUrl + ConfigName;
      w.DownloadFileAsync(new Uri(url), RedlineSettings.ProjectConfigPath + "update_" + ConfigName);
    }

    private static void ConfigDownloadCompleted(object sender, AsyncCompletedEventArgs e) {
      if (e.Error == null) {
        //var updateFile = File.ReadAllText(Redline_Settings.projectConfigPath + "update_" + configName);
        File.Delete(RedlineSettings.ProjectConfigPath + ConfigName);
        File.Move(RedlineSettings.ProjectConfigPath + "update_" + ConfigName,
          RedlineSettings.ProjectConfigPath + ConfigName);
        RedlinePackageManager.LoadJson();

        EditorPrefs.SetInt("Redline_configImportLastUpdated",
          (int) DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        RedlineLog("Import Config has been updated!");
      } else {
        RedlineLog("Import Config could not be updated!");
      }
    }

    private static void FileDownloadCompleted(object sender, AsyncCompletedEventArgs e) {
      var assetName = ((WebClient) sender).QueryString["assetName"];
      if (e.Error == null) {
        RedlineLog("Download of file " + assetName + " completed!");
      } else {
        DeleteAsset(assetName);
        RedlineLog("Download of file " + assetName + " failed!");
      }
    }

    private static void FileDownloadProgress(object sender, DownloadProgressChangedEventArgs e) {
      var progress = e.ProgressPercentage;
      var assetName = ((WebClient) sender).QueryString["assetName"];
      switch (progress) {
      case < 0:
        return;
      case >= 100:
        EditorUtility.ClearProgressBar();
        break;
      default:
        EditorUtility.DisplayProgressBar("Download of " + assetName,
          "Downloading " + assetName + ". Currently at: " + progress + "%",
          (progress / 100F));
        break;
      }
    }

    public static void CheckForConfigUpdate() {
      if (EditorPrefs.HasKey("Redline_configImportLastUpdated")) {
        var lastUpdated = EditorPrefs.GetInt("Redline_configImportLastUpdated");
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (currentTime - lastUpdated < 3600) {
          Debug.Log("Not updating config: " + (currentTime - lastUpdated));
          return;
        }
      }

      RedlineLog("Updating import config");
      UpdateConfig();
    }

    private static void RedlineLog(string message) {
      Debug.Log("[Redline] AssetDownloadManager: " + message);
    }

    private static void ImportDownloadedAsset(string assetName) {
      AssetDatabase.ImportPackage(RedlineSettings.GetAssetPath() + assetName, true);
    }
  }
}