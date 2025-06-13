using System;
using System.IO;
using UnityEngine;

namespace Redline.Runtime.DiscordRPC
{
    public static class DiscordRpcNativeLoader
    {
        private const string WindowsLibraryName = "discord-rpc.dll";
        private const string MacLibraryName = "libdiscord-rpc.dylib";
        private const string LinuxLibraryName = "discord-rpc.so";

        static DiscordRpcNativeLoader()
        {
            LoadNativeLibrary();
        }

        private static void LoadNativeLibrary()
        {
            try
            {
                string libraryPath = GetNativeLibraryPath();
                if (string.IsNullOrEmpty(libraryPath))
                {
                    Debug.LogError("[Redline] DiscordRPC: Failed to find native library path");
                    return;
                }

                Debug.Log($"[Redline] DiscordRPC: Loading native library from {libraryPath}");
                if (!File.Exists(libraryPath))
                {
                    Debug.LogError($"[Redline] DiscordRPC: Native library not found at {libraryPath}");
                    return;
                }

                // On Windows, we need to use LoadLibrary
                if (Application.platform == RuntimePlatform.WindowsEditor || 
                    Application.platform == RuntimePlatform.WindowsPlayer)
                {
                    if (NativeLibrary.LoadLibrary(libraryPath) == IntPtr.Zero)
                    {
                        Debug.LogError("[Redline] DiscordRPC: Failed to load Windows native library");
                    }
                    else
                    {
                        Debug.Log("[Redline] DiscordRPC: Successfully loaded Windows native library");
                    }
                }
                // On other platforms, the dynamic loader should handle it automatically
                // when the DllImport is called
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Redline] DiscordRPC: Error loading native library: {ex.Message}");
            }
        }

        private static string GetNativeLibraryPath()
        {
            string basePluginsPath = Path.Combine(Application.dataPath, "Packages/dev.redline-team.rpm/Plugins/x86_64");
            
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    return Path.Combine(basePluginsPath, "win", WindowsLibraryName);
                
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return Path.Combine(basePluginsPath, "osx", MacLibraryName);
                
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                    return Path.Combine(basePluginsPath, LinuxLibraryName);
                
                default:
                    Debug.LogError($"[Redline] DiscordRPC: Unsupported platform: {Application.platform}");
                    return null;
            }
        }
    }

    // Simple P/Invoke wrapper for Windows LoadLibrary
    internal static class NativeLibrary
    {
        [System.Runtime.InteropServices.DllImport("kernel32", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public static extern IntPtr LoadLibrary(string lpFileName);
    }
}
