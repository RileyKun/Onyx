using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Redline.Editor.VPM
{
    /// <summary>
    /// Represents a VPM Repository with its packages
    /// </summary>
    [Serializable]
    public class VPMRepository
    {
        public string Name;
        public string Author;
        public string Url;
        public string Id;
        public Dictionary<string, VPMPackage> Packages = new Dictionary<string, VPMPackage>();
        
        /// <summary>
        /// The local file path where this repository is stored
        /// </summary>
        [NonSerialized]
        public string LocalPath;

        /// <summary>
        /// Gets the path where VPM repositories should be stored
        /// </summary>
        private static string GetVPMRepositoriesPath()
        {
            string reposPath = RedlineSettings.GetRepositoriesPath();
            Directory.CreateDirectory(reposPath);
            return reposPath;
        }

        /// <summary>
        /// Gets the path to the marker file that indicates if default repositories have been imported
        /// </summary>
        private static string GetDefaultReposMarkerPath()
        {
            return Path.Combine(GetVPMRepositoriesPath(), ".default_repos_imported");
        }

        /// <summary>
        /// Checks if default repositories have been imported
        /// </summary>
        public static bool HaveDefaultRepositoriesBeenImported()
        {
            return File.Exists(GetDefaultReposMarkerPath());
        }

        /// <summary>
        /// Copies default repositories from the package to the user's repository directory
        /// </summary>
        /// <param name="force">If true, will copy repositories even if they've been imported before</param>
        public static void CopyDefaultRepositories(bool force = false)
        {
            // Get the project's root directory
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            string defaultReposPath = Path.Combine(projectPath, RedlineSettings.ProjectConfigPath, "VPM");
            string userReposPath = GetVPMRepositoriesPath();
            string markerPath = GetDefaultReposMarkerPath();

            // If we've already imported and not forcing, skip
            if (!force && HaveDefaultRepositoriesBeenImported())
            {
                Debug.Log("Default repositories have already been imported. Use force=true to reimport.");
                return;
            }

            if (!Directory.Exists(defaultReposPath))
            {
                Debug.LogWarning($"Default repositories path not found: {defaultReposPath}");
                return;
            }

            // Create the user's repository directory if it doesn't exist
            Directory.CreateDirectory(userReposPath);

            bool success = true;
            // Copy each repository file
            foreach (string repoFile in Directory.GetFiles(defaultReposPath, "*.json"))
            {
                string fileName = Path.GetFileName(repoFile);
                string targetPath = Path.Combine(userReposPath, fileName);

                // If forcing, we'll overwrite existing files
                if (force || !File.Exists(targetPath))
                {
                    try
                    {
                        File.Copy(repoFile, targetPath, force);
                        Debug.Log($"Copied default repository: {fileName}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to copy repository {fileName}: {ex.Message}");
                        success = false;
                    }
                }
            }

            // Only create the marker file if all copies were successful
            if (success)
            {
                try
                {
                    File.WriteAllText(markerPath, DateTime.Now.ToString("o"));
                    Debug.Log("Default repositories imported successfully");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to create marker file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Downloads a VPM repository from a URL and saves it to the local VPM directory
        /// </summary>
        /// <param name="url">The URL to download the repository from</param>
        /// <returns>The downloaded and parsed repository, or null if failed</returns>
        public static async Task<VPMRepository> DownloadFromUrl(string url)
        {
            Debug.Log($"Downloading repository from URL: {url}");
            try
            {
                // Create the directory if it doesn't exist
                string vpmDirectory = GetVPMRepositoriesPath();
                Debug.Log($"VPM Directory: {vpmDirectory}");
                
                if (!Directory.Exists(vpmDirectory))
                {
                    Debug.Log($"Creating VPM directory: {vpmDirectory}");
                    Directory.CreateDirectory(vpmDirectory);
                }
                else
                {
                    Debug.Log($"VPM directory already exists: {vpmDirectory}");
                }

                // Download the repository JSON
                Debug.Log("Downloading repository JSON...");
                using (WebClient client = new WebClient())
                {
                    string json = await Task.Run(() => client.DownloadString(url));
                    Debug.Log($"Downloaded JSON (first 100 chars): {json.Substring(0, Math.Min(100, json.Length))}...");
                    
                    // Parse the JSON
                    VPMRepository repository = ParseRepositoryJson(json);
                    
                    if (repository != null)
                    {
                        Debug.Log($"Repository parsed successfully. Name: {repository.Name}, Author: {repository.Author}");
                        
                        // Extract repository name from the URL if the name is missing
                        if (string.IsNullOrEmpty(repository.Name))
                        {
                            Debug.Log("Repository name is missing, extracting from URL...");
                            try
                            {
                                // Try to extract a meaningful name from the URL
                                Uri uri = new Uri(url);
                                string host = uri.Host;
                                Debug.Log($"URL host: {host}");
                                
                                // Use the domain name as part of the repository name
                                string[] hostParts = host.Split('.');
                                if (hostParts.Length >= 2)
                                {
                                    repository.Name = hostParts[hostParts.Length - 2];
                                    // Capitalize first letter
                                    repository.Name = char.ToUpper(repository.Name[0]) + repository.Name.Substring(1) + " Repo";
                                    Debug.Log($"Generated repository name from host: {repository.Name}");
                                }
                                else
                                {
                                    repository.Name = host + " Repo";
                                    Debug.Log($"Generated repository name from host: {repository.Name}");
                                }
                            }
                            catch (Exception ex)
                            {
                                // Fallback if URI parsing fails
                                Debug.LogError($"Failed to extract name from URL: {ex.Message}");
                                repository.Name = "Repository_" + DateTime.Now.Ticks;
                                Debug.Log($"Using fallback repository name: {repository.Name}");
                            }
                        }
                        
                        // Generate a filename based on the repository name
                        string repoName = repository.Name;
                        Debug.Log($"Using repository name for file: {repoName}");
                        
                        // Ensure the repository name is valid for a filename
                        // Keep spaces but replace invalid filename characters
                        foreach (char invalidChar in Path.GetInvalidFileNameChars())
                        {
                            if (invalidChar != ' ') // Keep spaces
                            {
                                repoName = repoName.Replace(invalidChar, '_');
                            }
                        }
                        
                        string filePath = Path.Combine(vpmDirectory, repoName + ".json");
                        Debug.Log($"Sanitized file name: {repoName}.json");
                        Debug.Log($"Full file path for saving: {filePath}");
                        
                        // Save the formatted JSON to disk
                        string formattedJson = JsonConvert.SerializeObject(repository, Formatting.Indented);
                        Debug.Log($"Saving JSON to file: {filePath}");
                        
                        try {
                            File.WriteAllText(filePath, formattedJson);
                            Debug.Log($"Successfully saved repository to: {filePath}");
                            
                            // Verify the file was created
                            if (File.Exists(filePath)) {
                                Debug.Log($"Verified file exists at: {filePath}");
                            } else {
                                Debug.LogError($"File was not created at: {filePath}");
                            }
                        } catch (Exception ex) {
                            Debug.LogError($"Failed to write file: {ex.Message}");
                        }
                        
                        repository.LocalPath = filePath;
                        return repository;
                    }
                    else
                    {
                        Debug.LogError("Failed to parse repository JSON");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to download repository from {url}: {e.Message}");
                Debug.LogException(e);
            }
            
            return null;
        }

        /// <summary>
        /// Parses a JSON string into a VPMRepository object
        /// </summary>
        /// <param name="json">The JSON string to parse</param>
        /// <returns>The parsed repository, or null if parsing failed</returns>
        public static VPMRepository ParseRepositoryJson(string json)
        {
            try
            {
                Debug.Log($"Parsing JSON: {json.Substring(0, Math.Min(100, json.Length))}...");
                JObject repoJson = JObject.Parse(json);
                
                // Helper function to get property with case insensitivity
                string GetPropertyCaseInsensitive(JObject obj, string propertyName)
                {
                    // Try exact match first
                    if (obj[propertyName] != null)
                        return obj[propertyName].ToString();
                    
                    // Try case-insensitive match
                    foreach (var prop in obj.Properties())
                    {
                        if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                            return prop.Value.ToString();
                    }
                    
                    return null;
                }
                
                VPMRepository repository = new VPMRepository
                {
                    Name = GetPropertyCaseInsensitive(repoJson, "name"),
                    Author = GetPropertyCaseInsensitive(repoJson, "author"),
                    Url = GetPropertyCaseInsensitive(repoJson, "url"),
                    Id = GetPropertyCaseInsensitive(repoJson, "id"),
                    Packages = new Dictionary<string, VPMPackage>()
                };
                
                Debug.Log($"Parsed repository: Name={repository.Name}, Author={repository.Author}, Url={repository.Url}, Id={repository.Id}");

                // Parse packages - look for both "packages" and "Packages"
                JObject packages = repoJson["packages"] as JObject ?? repoJson["Packages"] as JObject;
                if (packages != null)
                {
                    Debug.Log($"Found packages section with {packages.Count} packages");
                    foreach (var packagePair in packages)
                    {
                        string packageId = packagePair.Key;
                        JObject packageObj = packagePair.Value as JObject;
                        
                        if (packageObj != null)
                        {
                            VPMPackage package = new VPMPackage
                            {
                                Id = packageId,
                                Versions = new Dictionary<string, VPMPackageVersion>()
                            };

                            // Parse versions - look for both "versions" and "Versions"
                            JObject versions = packageObj["versions"] as JObject ?? packageObj["Versions"] as JObject;
                            if (versions != null)
                            {
                                Debug.Log($"Found versions section with {versions.Count} versions for package {packageId}");
                                foreach (var versionPair in versions)
                                {
                                    string versionString = versionPair.Key;
                                    JObject versionObj = versionPair.Value as JObject;
                                    
                                    if (versionObj != null)
                                    {
                                        VPMPackageVersion version = new VPMPackageVersion
                                        {
                                            Name = GetPropertyCaseInsensitive(versionObj, "name"),
                                            DisplayName = GetPropertyCaseInsensitive(versionObj, "displayName"),
                                            Version = GetPropertyCaseInsensitive(versionObj, "version"),
                                            Unity = GetPropertyCaseInsensitive(versionObj, "unity"),
                                            Description = GetPropertyCaseInsensitive(versionObj, "description"),
                                            ChangelogUrl = GetPropertyCaseInsensitive(versionObj, "changelogUrl"),
                                            Url = GetPropertyCaseInsensitive(versionObj, "url"),
                                            ZipSHA256 = GetPropertyCaseInsensitive(versionObj, "zipSHA256")
                                        };
                                        
                                        // Parse author if available - look for both "author" and "Author"
                                        JObject authorObj = versionObj["author"] as JObject ?? versionObj["Author"] as JObject;
                                        if (authorObj != null)
                                        {
                                            version.AuthorName = GetPropertyCaseInsensitive(authorObj, "name");
                                            version.AuthorUrl = GetPropertyCaseInsensitive(authorObj, "url");
                                        }
                                        
                                        package.Versions[versionString] = version;
                                    }
                                }
                            }
                            
                            repository.Packages[packageId] = package;
                        }
                    }
                }
                
                return repository;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse repository JSON: {e.Message}");
                Debug.LogException(e);
                return null;
            }
        }

        /// <summary>
        /// Loads a repository from a local file
        /// </summary>
        /// <param name="filePath">The path to the repository JSON file</param>
        /// <returns>The loaded repository, or null if loading failed</returns>
        public static VPMRepository LoadFromFile(string filePath)
        {
            Debug.Log($"Loading repository from file: {filePath}");
            try
            {
                if (File.Exists(filePath))
                {
                    Debug.Log($"File exists: {filePath}");
                    
                    // Check if file is empty
                    if (new FileInfo(filePath).Length == 0)
                    {
                        Debug.LogError($"Repository file is empty: {filePath}");
                        return null;
                    }

                    // Try to read the file and check if it's valid text
                    string json;
                    try
                    {
                        json = File.ReadAllText(filePath);
                        if (string.IsNullOrWhiteSpace(json))
                        {
                            Debug.LogError($"Repository file contains only whitespace: {filePath}");
                            return null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to read repository file (file may be corrupted): {filePath}\nError: {ex.Message}");
                        // Try to delete the corrupted file
                        try
                        {
                            File.Delete(filePath);
                            Debug.Log($"Deleted corrupted repository file: {filePath}");
                        }
                        catch (Exception deleteEx)
                        {
                            Debug.LogError($"Failed to delete corrupted repository file: {filePath}\nError: {deleteEx.Message}");
                        }
                        return null;
                    }

                    Debug.Log($"Read {json.Length} characters from file");
                    
                    VPMRepository repository = ParseRepositoryJson(json);
                    if (repository != null)
                    {
                        Debug.Log($"Successfully parsed repository: {repository.Name}");
                        repository.LocalPath = filePath;
                        return repository;
                    }
                    else
                    {
                        Debug.LogError($"Failed to parse repository from file: {filePath}");
                        // Try to delete the invalid file
                        try
                        {
                            File.Delete(filePath);
                            Debug.Log($"Deleted invalid repository file: {filePath}");
                        }
                        catch (Exception deleteEx)
                        {
                            Debug.LogError($"Failed to delete invalid repository file: {filePath}\nError: {deleteEx.Message}");
                        }
                    }
                }
                else
                {
                    Debug.LogError($"File does not exist: {filePath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load repository from {filePath}: {e.Message}");
                Debug.LogException(e);
            }
            
            return null;
        }
    }
}
