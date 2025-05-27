using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Redline.Editor.VPM
{
    /// <summary>
    /// Editor window for managing VPM repositories and packages
    /// </summary>
    public class VPMWindow : EditorWindow
    {
        private enum Tab
        {
            Repositories,
            Packages,
            Installed
        }
        
        // Banner style
        private GUIStyle _bannerStyle;

        private Tab _currentTab = Tab.Repositories;
        private Vector2 _repositoriesScrollPosition;
        private Vector2 _packagesScrollPosition;
        private Vector2 _installedScrollPosition;
        private string _newRepositoryUrl = "";
        private bool _isAddingRepository = false;
        private float _installProgress = 0f;
        private bool _isInstallingPackage = false;
        private string _currentInstallingPackage = "";
        private string _searchFilter = "";
        private Dictionary<string, bool> _repositoryFoldouts = new Dictionary<string, bool>();
        
        // Dictionary to track selected version for each package
        private Dictionary<string, string> _selectedVersions = new Dictionary<string, string>();
        
        // List of installed packages
        private List<InstalledPackageInfo> _installedPackages = new List<InstalledPackageInfo>();
        private bool _isRemovingPackage = false;
        private string _currentRemovingPackage = "";
        
        // Options
        private bool _showUnstableReleases = false;
        private bool _isRefreshingRepositories = false;
        private bool _showDebugOptions = false; // Whether to show the debug options section
        
        // Repository tab selection
        private string _selectedRepositoryId = "all"; // ID of the currently selected repository tab ("all" for All Packages)
        private string _selectedRepositoryGroup = "none"; // Group of the currently selected repository ("none" for no group)
        private Dictionary<string, bool> _repositoryGroupFoldouts = new Dictionary<string, bool>(); // Track which repository groups are expanded

        // Cached data
        private List<VPMRepository> _repositories = new List<VPMRepository>();
        private List<Tuple<VPMRepository, VPMPackage, VPMPackageVersion>> _filteredPackages = new List<Tuple<VPMRepository, VPMPackage, VPMPackageVersion>>();

        [MenuItem("Redline/VPM Repository Manager")]
        public static void ShowWindow()
        {
            VPMWindow window = GetWindow<VPMWindow>("VPM Manager");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            // Initialize the VPM manager
            VPMManager.Initialize();
            RefreshRepositories();
            RefreshPackages();
            RefreshInstalledPackages();
            
            // Initialize the banner style
            _bannerStyle = new GUIStyle
            {
                normal = {
                    background = Resources.Load("RedlinePMHeader") as Texture2D,
                    textColor = Color.white
                },
                // No fixed height - will be calculated dynamically to maintain aspect ratio
            };
        }

        private void OnGUI()
        {
            // Wrap everything in a try-catch to prevent editor crashes
            try
            {
                DrawToolbar();

                EditorGUILayout.Space();

                switch (_currentTab)
                {
                    case Tab.Repositories:
                        DrawRepositoriesTab();
                        break;
                    case Tab.Packages:
                        DrawPackagesTab();
                        break;
                    case Tab.Installed:
                        DrawInstalledTab();
                        break;
                }
            }
            catch (System.Exception e)
            {
                // Log the error but don't crash the editor
                Debug.LogError($"Error in VPM Window: {e.Message}\n{e.StackTrace}");
            }

            // Draw installation progress if a package is being installed
            if (_isInstallingPackage)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Installing {_currentInstallingPackage}...");
                Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(progressRect, _installProgress, $"{Mathf.Round(_installProgress * 100)}%");
            }
            
            // Draw removal progress if a package is being removed
            if (_isRemovingPackage)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Removing {_currentRemovingPackage}...");
            }
        }

        private void DrawToolbar()
        {
            // Draw the Redline banner at the top with dynamic height to maintain aspect ratio
            if (_bannerStyle != null && _bannerStyle.normal.background != null)
            {
                Texture2D headerTexture = _bannerStyle.normal.background;
                // Original aspect ratio is 1024:217
                float aspectRatio = 1024f / 217f;
                // Calculate height based on current window width to maintain aspect ratio
                float width = EditorGUIUtility.currentViewWidth;
                float height = width / aspectRatio;
                
                // Draw the banner with calculated height
                Rect bannerRect = GUILayoutUtility.GetRect(width, height);
                GUI.Box(bannerRect, "", _bannerStyle);
                EditorGUILayout.Space(5); // Add a small space after the banner
            }
            
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Toggle(_currentTab == Tab.Repositories, "Repositories", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                _currentTab = Tab.Repositories;
            }

            if (GUILayout.Toggle(_currentTab == Tab.Packages, "Packages", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                _currentTab = Tab.Packages;
            }
            
            if (GUILayout.Toggle(_currentTab == Tab.Installed, "Installed", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                _currentTab = Tab.Installed;
                RefreshInstalledPackages(); // Refresh the list when switching to this tab
            }

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(_isRefreshingRepositories);
            if (GUILayout.Button(_isRefreshingRepositories ? "Refreshing..." : "Refresh All", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                RefreshRepositoriesFromUrlsAsync();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRepositoriesTab()
        {
            EditorGUILayout.LabelField("VPM Repositories", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Add new repository section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Add New Repository", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            _newRepositoryUrl = EditorGUILayout.TextField("Repository URL", _newRepositoryUrl);
            EditorGUI.BeginDisabledGroup(_isAddingRepository || string.IsNullOrEmpty(_newRepositoryUrl));
            if (GUILayout.Button("Add", GUILayout.Width(60)))
            {
                AddRepositoryAsync(_newRepositoryUrl);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            
            // Add import from VCC/ALCOM button
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Import from VCC/ALCOM", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Import repositories from your VRChat Creator Companion or ALCOM installation.", MessageType.Info);
            
            EditorGUI.BeginDisabledGroup(_isAddingRepository);
            if (GUILayout.Button("Import Repositories", GUILayout.Height(30)))
            {
                ImportVCCRepositoriesAsync();
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // List of repositories
            EditorGUILayout.LabelField("Available Repositories", EditorStyles.boldLabel);
            
            _repositoriesScrollPosition = EditorGUILayout.BeginScrollView(_repositoriesScrollPosition);
            
            if (_repositories.Count == 0)
            {
                EditorGUILayout.HelpBox("No repositories added yet. Add a repository using the URL field above.", MessageType.Info);
            }
            else
            {
                foreach (VPMRepository repository in _repositories)
                {
                    DrawRepositoryItem(repository);
                }
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawRepositoryItem(VPMRepository repository)
        {
            // Generate a unique key for the repository that won't be null
            string repoKey = repository.Id ?? repository.Name ?? repository.Url ?? ("repo_" + repository.GetHashCode());
            
            if (!_repositoryFoldouts.ContainsKey(repoKey))
            {
                _repositoryFoldouts[repoKey] = false;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            _repositoryFoldouts[repoKey] = EditorGUILayout.Foldout(_repositoryFoldouts[repoKey], repository.Name ?? "Unnamed Repository", true);
            
            if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Remove Repository", $"Are you sure you want to remove {repository.Name}?", "Yes", "No"))
                {
                    VPMManager.RemoveRepository(repository);
                    RefreshRepositories();
                    RefreshPackages();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_repositoryFoldouts[repoKey])
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.LabelField("ID", repository.Id ?? "N/A");
                EditorGUILayout.LabelField("Author", repository.Author ?? "N/A");
                EditorGUILayout.LabelField("URL", repository.Url ?? "N/A");
                EditorGUILayout.LabelField("Packages", repository.Packages?.Count.ToString() ?? "0");
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawPackagesTab()
        {
            EditorGUILayout.LabelField("Available Packages", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Search field
            EditorGUILayout.BeginHorizontal();
            string newSearchFilter = EditorGUILayout.TextField("Search", _searchFilter);
            if (newSearchFilter != _searchFilter)
            {
                _searchFilter = newSearchFilter;
                RefreshPackages();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            
            // Repository tabs
            DrawRepositoryTabs();
            
            EditorGUILayout.Space();

            // List of packages
            _packagesScrollPosition = EditorGUILayout.BeginScrollView(_packagesScrollPosition);
            
            // Filter packages based on selected repository
            List<Tuple<VPMRepository, VPMPackage, VPMPackageVersion>> packagesToShow = _filteredPackages;
            if (_selectedRepositoryId != "all")
            {
                packagesToShow = _filteredPackages.Where(p => p.Item1.Id == _selectedRepositoryId).ToList();
            }
            
            if (packagesToShow.Count == 0)
            {
                if (_filteredPackages.Count == 0)
                {
                    EditorGUILayout.HelpBox("No packages found. Add repositories to see available packages.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("No packages found in this repository that match your search criteria.", MessageType.Info);
                }
            }
            else
            {
                foreach (var packageTuple in packagesToShow)
                {
                    DrawPackageItem(packageTuple.Item1, packageTuple.Item2, packageTuple.Item3);
                }
            }
            
            EditorGUILayout.EndScrollView();
            
            // Options section at the bottom
            EditorGUILayout.BeginHorizontal();
            
            // Debug options toggle (left side)
            _showDebugOptions = EditorGUILayout.ToggleLeft("Debug Options", _showDebugOptions, GUILayout.Width(100));
            
            GUILayout.FlexibleSpace(); // Push the unstable releases toggle to the right
            
            // Unstable releases toggle (right side)
            bool newShowUnstable = EditorGUILayout.Toggle("Show Unstable Releases", _showUnstableReleases);
            if (newShowUnstable != _showUnstableReleases)
            {
                _showUnstableReleases = newShowUnstable;
                // Refresh packages to apply the filter
                RefreshPackages();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Debug options section (only shown if debug options are enabled)
            if (_showDebugOptions)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Debug Settings", EditorStyles.boldLabel);
                
                // Debug logging toggle
                bool enableLogging = EditorGUILayout.Toggle("Enable Debug Logging", VPMSettings.EnableDebugLogging);
                if (enableLogging != VPMSettings.EnableDebugLogging)
                {
                    VPMSettings.EnableDebugLogging = enableLogging;
                }
                
                EditorGUILayout.HelpBox("Enabling debug logging will output detailed information about version filtering to the console. This may impact performance if there are many packages.", MessageType.Info);
                
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawPackageItem(VPMRepository repository, VPMPackage package, VPMPackageVersion latestVersion)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Get unique package identifier
            string packageKey = package.Id;
            
            // Initialize selected version if not already set
            if (!_selectedVersions.ContainsKey(packageKey))
            {
                _selectedVersions[packageKey] = latestVersion.Version; // Default to latest version
            }
            
            // Get the currently selected version
            string selectedVersionString = _selectedVersions[packageKey];
            
            // Make sure the selected version exists in the package
            if (!package.Versions.ContainsKey(selectedVersionString))
            {
                selectedVersionString = latestVersion.Version;
                _selectedVersions[packageKey] = selectedVersionString;
            }
            
            // Get the selected version object
            VPMPackageVersion selectedVersion = package.Versions[selectedVersionString];
            
            // Package name and version selection UI
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(package.GetDisplayName(), EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(_isInstallingPackage);
            
            // Create a list of available versions
            List<string> versionOptions = package.Versions.Keys.ToList();
            
            // Sort versions in descending order (newest first)
            versionOptions.Sort((a, b) => {
                try {
                    // Try to parse as Version objects for proper comparison
                    Version vA = new Version(a.Split('-')[0]);
                    Version vB = new Version(b.Split('-')[0]);
                    return vB.CompareTo(vA); // Descending order
                }
                catch {
                    // Fallback to string comparison if parsing fails
                    return string.Compare(b, a);
                }
            });
            
            // Find the index of the currently selected version
            int selectedIndex = versionOptions.IndexOf(selectedVersionString);
            if (selectedIndex < 0) selectedIndex = 0; // Default to first version if not found
            
            // Draw the dropdown
            int newSelectedIndex = EditorGUILayout.Popup(selectedIndex, versionOptions.ToArray(), GUILayout.Width(100));
            
            // Update selected version if changed
            if (newSelectedIndex != selectedIndex)
            {
                _selectedVersions[packageKey] = versionOptions[newSelectedIndex];
                // Update the selected version object
                selectedVersionString = versionOptions[newSelectedIndex];
                selectedVersion = package.Versions[selectedVersionString];
            }
            
            // Install button
            if (GUILayout.Button("Install", GUILayout.Width(60)))
            {
                InstallPackageAsync(selectedVersion);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            
            // Display package details
            EditorGUILayout.LabelField("Repository", repository.Name ?? "Unknown Repository");
            EditorGUILayout.LabelField("Version", selectedVersion.Version ?? "Unknown Version");
            EditorGUILayout.LabelField("Unity", selectedVersion.Unity ?? "Any Unity Version");
            
            // Show description and author from the selected version
            if (!string.IsNullOrEmpty(selectedVersion.Description))
            {
                EditorGUILayout.LabelField("Description", selectedVersion.Description);
            }
            
            if (!string.IsNullOrEmpty(selectedVersion.AuthorName))
            {
                EditorGUILayout.LabelField("Author", selectedVersion.AuthorName);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private async void AddRepositoryAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;

            _isAddingRepository = true;
            bool success = await VPMManager.AddRepositoryFromUrl(url);
            _isAddingRepository = false;

            if (success)
            {
                _newRepositoryUrl = "";
                RefreshRepositories();
                RefreshPackages();
                Debug.Log($"Successfully added repository from {url}");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", $"Failed to add repository from {url}. Please check the URL and try again.", "OK");
            }

            Repaint();
        }

        private async void RefreshRepositoriesFromUrlsAsync()
        {
            _isRefreshingRepositories = true;
            int refreshedCount = await VPMManager.RefreshRepositoriesFromUrls();
            _isRefreshingRepositories = false;
            RefreshRepositories();
            
            if (refreshedCount > 0)
            {
                RefreshPackages();
                EditorUtility.DisplayDialog("Refresh Complete", $"Successfully refreshed {refreshedCount} repositories.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Refresh Complete", "No repositories were refreshed. Make sure your repositories are accessible.", "OK");
            }
        }
        
        private async void ImportVCCRepositoriesAsync()
        {
            try
            {
                _isAddingRepository = true;
                Repaint(); // Refresh the UI to show loading state
                
                int importedCount = await VPMManager.ImportVCCRepositories();
                
                RefreshRepositories();
                RefreshPackages();
                
                if (importedCount > 0)
                {
                    EditorUtility.DisplayDialog("Import Complete", $"Successfully imported {importedCount} repositories from VCC/ALCOM.", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Import Complete", 
                        "No new repositories were found in your VCC/ALCOM installation or they already exist in RPM.\n\n" +
                        "Make sure you have VCC or ALCOM installed and have added repositories to it.", 
                        "OK");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error importing VCC/ALCOM repositories: {e}");
                EditorUtility.DisplayDialog("Import Failed", 
                    $"An error occurred while importing repositories: {e.Message}", 
                    "OK");
            }
            finally
            {
                _isAddingRepository = false;
                Repaint();
            }
        }

        private async void InstallPackageAsync(VPMPackageVersion packageVersion)
        {
            if (packageVersion == null)
                return;

            _isInstallingPackage = true;
            _installProgress = 0f;
            _currentInstallingPackage = packageVersion.DisplayName ?? packageVersion.Name;

            bool success = await VPMManager.InstallPackage(packageVersion, 
                progress => {
                    _installProgress = progress;
                    Repaint();
                },
                isComplete => {
                    _isInstallingPackage = false;
                    Repaint();
                });

            // Always refresh after installation completes, regardless of success
            VPMManager.LoadAllRepositories();
            RefreshRepositories();
            RefreshPackages();
            RefreshInstalledPackages(); // Also refresh installed packages

            if (success)
            {
                Debug.Log($"Successfully installed {packageVersion.Name} {packageVersion.Version}");
            }
            else
            {
                EditorUtility.DisplayDialog("Installation Failed", $"Failed to install {packageVersion.Name} {packageVersion.Version}. Check the console for details.", "OK");
            }

            Repaint();
        }

        private void RefreshRepositories()
        {
            _repositories = VPMManager.GetRepositories();
        }

        /// <summary>
        /// Draws the repository tabs at the top of the packages tab
        /// </summary>
        private void DrawRepositoryTabs()
        {
            // Calculate how many tabs we need to display
            int tabCount = _repositories.Count + 1; // +1 for "All Packages" tab
            
            // Don't show tabs if there's only one repository
            if (tabCount <= 2)
            {
                return;
            }
            
            // Group repositories by their provider
            Dictionary<string, List<VPMRepository>> repositoryGroups = new Dictionary<string, List<VPMRepository>>();
            
            // Add repository groups
            repositoryGroups["VRChat"] = new List<VPMRepository>();
            repositoryGroups["Community"] = new List<VPMRepository>();
            
            // Categorize repositories
            foreach (VPMRepository repo in _repositories)
            {
                // Skip repositories without an ID
                if (string.IsNullOrEmpty(repo.Id))
                    continue;
                
                // Group VRChat repositories
                if (repo.Id == "com.vrchat.repos.official" || repo.Id == "com.vrchat.repos.curated")
                {
                    repositoryGroups["VRChat"].Add(repo);
                }
                else
                {
                    // All other repositories go to Community group
                    repositoryGroups["Community"].Add(repo);
                }
            }
            
            // Remove empty groups
            List<string> emptyGroups = new List<string>();
            foreach (var group in repositoryGroups)
            {
                if (group.Value.Count == 0)
                {
                    emptyGroups.Add(group.Key);
                }
            }
            foreach (string groupName in emptyGroups)
            {
                repositoryGroups.Remove(groupName);
            }
            
            // Draw main tabs
            EditorGUILayout.BeginVertical();
            
            // First row - Main tabs
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // "All Packages" tab
            bool allSelected = GUILayout.Toggle(_selectedRepositoryId == "all" && _selectedRepositoryGroup == "none", 
                "All Packages", EditorStyles.toolbarButton);
            if (allSelected && (_selectedRepositoryId != "all" || _selectedRepositoryGroup != "none"))
            {
                _selectedRepositoryId = "all";
                _selectedRepositoryGroup = "none";
            }
            
            // Group tabs
            foreach (var group in repositoryGroups)
            {
                string groupName = group.Key;
                
                // Ensure the group has an entry in the foldout dictionary
                if (!_repositoryGroupFoldouts.ContainsKey(groupName))
                {
                    _repositoryGroupFoldouts[groupName] = false;
                }
                
                // Draw the group tab
                bool groupSelected = GUILayout.Toggle(_selectedRepositoryGroup == groupName, 
                    groupName, EditorStyles.toolbarButton);
                    
                if (groupSelected && _selectedRepositoryGroup != groupName)
                {
                    _selectedRepositoryGroup = groupName;
                    _selectedRepositoryId = "group"; // Special ID to indicate we're viewing a group
                    _repositoryGroupFoldouts[groupName] = true; // Expand the group
                }
            }
            
            // No more ungrouped repositories - all are now in groups
            
            EditorGUILayout.EndHorizontal();
            
            // Second row - Subtabs for selected group
            if (_selectedRepositoryGroup != "none" && repositoryGroups.ContainsKey(_selectedRepositoryGroup))
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                
                // Add some indentation for subtabs
                GUILayout.Space(15);
                
                // Draw subtabs for the selected group
                foreach (VPMRepository repo in repositoryGroups[_selectedRepositoryGroup])
                {
                    string subtabLabel;
                    
                    // Special labels for VRChat repositories
                    if (repo.Id == "com.vrchat.repos.official")
                    {
                        subtabLabel = "Official";
                    }
                    else if (repo.Id == "com.vrchat.repos.curated")
                    {
                        subtabLabel = "Curated";
                    }
                    else
                    {
                        subtabLabel = !string.IsNullOrEmpty(repo.Name) ? repo.Name : repo.Id;
                    }
                    
                    // Draw the subtab
                    bool selected = GUILayout.Toggle(_selectedRepositoryId == repo.Id, 
                        subtabLabel, EditorStyles.toolbarButton);
                        
                    if (selected && _selectedRepositoryId != repo.Id)
                    {
                        _selectedRepositoryId = repo.Id;
                    }
                }
                
                // Fill the rest of the space
                GUILayout.FlexibleSpace();
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void RefreshPackages()
        {
            _filteredPackages.Clear();

            foreach (VPMRepository repository in _repositories)
            {
                if (repository.Packages != null)
                {
                    foreach (var packagePair in repository.Packages)
                    {
                        VPMPackage package = packagePair.Value;
                        
                        // Get the latest version based on the unstable setting
                        VPMPackageVersion latestVersion = package.GetLatestVersion(_showUnstableReleases);

                        if (latestVersion != null)
                        {
                            // Apply search filter if any
                            if (string.IsNullOrEmpty(_searchFilter) ||
                                latestVersion.DisplayName?.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                package.Id?.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                latestVersion.Description?.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                _filteredPackages.Add(new Tuple<VPMRepository, VPMPackage, VPMPackageVersion>(repository, package, latestVersion));
                            }
                        }
                    }
                }
            }

            // Sort packages by display name
            _filteredPackages = _filteredPackages.OrderBy(p => p.Item2.GetDisplayNameWithVersion()).ToList();
        }
        
        // Removed duplicate RefreshRepositoriesFromUrlsAsync method
        /// <summary>
        /// Refreshes the list of installed packages
        /// </summary>
        private void RefreshInstalledPackages()
        {
            _installedPackages.Clear();
            
            string packagesDir = VPMManager.GetPackagesDirectory();
            if (!Directory.Exists(packagesDir))
                return;
                
            // Get all directories in the Packages folder
            string[] packageDirs = Directory.GetDirectories(packagesDir);
            
            foreach (string packageDir in packageDirs)
            {
                // Skip built-in Unity packages that start with "com.unity"
                string dirName = Path.GetFileName(packageDir);
                if (dirName.StartsWith("com.unity."))
                    continue;
                    
                // Check for package.json file
                string packageJsonPath = Path.Combine(packageDir, "package.json");
                if (File.Exists(packageJsonPath))
                {
                    try
                    {
                        // Read and parse the package.json file
                        string jsonContent = File.ReadAllText(packageJsonPath);
                        InstalledPackageInfo packageInfo = JsonUtility.FromJson<InstalledPackageInfo>(jsonContent);
                        
                        // Set the directory path for later use
                        packageInfo.DirectoryPath = packageDir;
                        
                        _installedPackages.Add(packageInfo);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error parsing package.json in {packageDir}: {e.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Draws the installed packages tab
        /// </summary>
        private void DrawInstalledTab()
        {
            EditorGUILayout.LabelField("Installed Packages", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Button to refresh the list
            if (GUILayout.Button("Refresh List", GUILayout.Width(100)))
            {
                RefreshInstalledPackages();
            }
            
            EditorGUILayout.Space();
            
            // List of installed packages
            _installedScrollPosition = EditorGUILayout.BeginScrollView(_installedScrollPosition);
            
            if (_installedPackages.Count == 0)
            {
                EditorGUILayout.HelpBox("No VPM packages are currently installed.", MessageType.Info);
            }
            else
            {
                foreach (InstalledPackageInfo package in _installedPackages)
                {
                    DrawInstalledPackageItem(package);
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        /// <summary>
        /// Draws an installed package item in the list
        /// </summary>
        private void DrawInstalledPackageItem(InstalledPackageInfo package)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            string displayName = !string.IsNullOrEmpty(package.displayName) ? package.displayName : package.name;
            EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);
            
            // Check if this is the VRChat base package and if we can remove it
            bool canRemove = true;
            string disabledReason = "";
            
            if (package.name == "com.vrchat.base")
            {
                // Check if any VRChat SDK packages are installed
                bool avatarsInstalled = _installedPackages.Any(p => p.name == "com.vrchat.avatars");
                bool worldsInstalled = _installedPackages.Any(p => p.name == "com.vrchat.worlds");
                
                if (avatarsInstalled || worldsInstalled)
                {
                    canRemove = false;
                    disabledReason = $"Cannot remove VRChat Base while {(avatarsInstalled ? "Avatars" : "Worlds")} SDK is installed";
                }
            }
            else if (package.name == "dev.redline-team.rpm")
            {
                // Prevent Redline Package Manager from removing itself
                canRemove = false;
                disabledReason = "Cannot remove Redline Package Manager (RPM) as it cannot uninstall itself";
            }
            
            EditorGUI.BeginDisabledGroup(_isRemovingPackage || !canRemove);
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Remove Package", $"Are you sure you want to remove {displayName}?", "Yes", "No"))
                {
                    RemovePackageAsync(package);
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            
            // Show warning if package cannot be removed
            if (!canRemove)
            {
                EditorGUILayout.HelpBox(disabledReason, MessageType.Warning);
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Package ID", package.name);
            EditorGUILayout.LabelField("Version", package.version);
            
            if (!string.IsNullOrEmpty(package.description))
            {
                EditorGUILayout.LabelField("Description", package.description);
            }
            
            if (!string.IsNullOrEmpty(package.author))
            {
                EditorGUILayout.LabelField("Author", package.author);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
        
        /// <summary>
        /// Removes an installed package
        /// </summary>
        private async void RemovePackageAsync(InstalledPackageInfo package)
        {
            if (package == null || string.IsNullOrEmpty(package.DirectoryPath))
                return;
            
            // Double-check VRChat package dependencies before removal
            if (package.name == "com.vrchat.base")
            {
                // Check if any VRChat SDK packages are installed
                bool avatarsInstalled = _installedPackages.Any(p => p.name == "com.vrchat.avatars");
                bool worldsInstalled = _installedPackages.Any(p => p.name == "com.vrchat.worlds");
                
                if (avatarsInstalled || worldsInstalled)
                {
                    string sdkName = avatarsInstalled ? "Avatars" : "Worlds";
                    EditorUtility.DisplayDialog("Cannot Remove Package", 
                        $"VRChat Base cannot be removed while the {sdkName} SDK is installed. Please remove the {sdkName} SDK first.", "OK");
                    return;
                }
            }
                
            _isRemovingPackage = true;
            _currentRemovingPackage = !string.IsNullOrEmpty(package.displayName) ? package.displayName : package.name;
            
            await Task.Run(() => {
                try
                {
                    // Delete the package directory
                    if (Directory.Exists(package.DirectoryPath))
                    {
                        Directory.Delete(package.DirectoryPath, true);
                        
                        // Also delete the .meta file if it exists
                        string metaFilePath = package.DirectoryPath + ".meta";
                        if (File.Exists(metaFilePath))
                        {
                            File.Delete(metaFilePath);
                        }
                    }
                    
                    Debug.Log($"Successfully removed package: {package.name}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to remove package {package.name}: {e.Message}");
                    EditorUtility.DisplayDialog("Error", $"Failed to remove package {package.name}. Check the console for details.", "OK");
                }
            });
            
            _isRemovingPackage = false;
            
            // Refresh AssetDatabase to detect the removed package
            AssetDatabase.Refresh();
            
            // Refresh the list of installed packages
            RefreshInstalledPackages();
            
            Repaint();
        }
    }
    
    /// <summary>
    /// Represents information about an installed package from package.json
    /// </summary>
    [Serializable]
    public class InstalledPackageInfo
    {
        public string name;
        public string displayName;
        public string version;
        public string description;
        public string author;
        
        // Not part of package.json, used to store the directory path
        [NonSerialized]
        public string DirectoryPath;
    }
}
