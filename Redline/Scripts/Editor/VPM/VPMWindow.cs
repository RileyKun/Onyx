using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

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

        private Dictionary<string, bool> _packageFoldouts = new Dictionary<string, bool>();

        private Dictionary<string, HashSet<string>> _packageDependencies = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, HashSet<string>> _reverseDependencies = new Dictionary<string, HashSet<string>>();

        // Cache for fun messages
        private Dictionary<string, string> _cachedFunMessages = new Dictionary<string, string>();

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
            RefreshPackageDependencies();
            
            // Initialize the banner style
            _bannerStyle = new GUIStyle
            {
                normal = {
                    background = Resources.Load("RedlinePMHeader") as Texture2D,
                    textColor = Color.white
                },
                // No fixed height - will be calculated dynamically to maintain aspect ratio
            };

            // Generate initial fun messages
            GenerateFunMessages();
        }

        /// <summary>
        /// Generates new fun messages for all states
        /// </summary>
        private void GenerateFunMessages()
        {
            _cachedFunMessages.Clear();
            _cachedFunMessages["no_upgrades"] = GetFunMessage("no_upgrades");
            _cachedFunMessages["no_packages"] = GetFunMessage("no_packages");
            _cachedFunMessages["no_search_results"] = GetFunMessage("no_search_results");
            _cachedFunMessages["no_repositories"] = GetFunMessage("no_repositories");
        }

        private async void OnGUI()
        {
            // Wrap everything in a try-catch to prevent editor crashes
            try
            {
                await DrawToolbar();

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

        private async Task DrawToolbar()
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

            // Sync VPM-Manifest versions
            if (GUILayout.Button("Sync Manifest", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                await ScanManifestAsync();
            }

            EditorGUI.BeginDisabledGroup(_isRefreshingRepositories);
            // Replace text button with icon button
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Refresh").image, 
                _isRefreshingRepositories ? "Refreshing..." : "Refresh All Repositories"), 
                EditorStyles.toolbarButton, GUILayout.Width(30)))
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
            EditorGUILayout.Space(5);
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
                DrawFunMessage(_cachedFunMessages["no_repositories"], new Color(0.8f, 0.4f, 0.2f)); // Orange color
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
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("ID", repository.Id ?? "N/A");
                EditorGUILayout.LabelField("Author", repository.Author ?? "N/A");
                EditorGUILayout.LabelField("URL", repository.Url ?? "N/A");
                EditorGUILayout.LabelField("Packages", repository.Packages?.Count.ToString() ?? "0");
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
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
                // Generate new search message when search changes
                _cachedFunMessages["no_search_results"] = GetFunMessage("no_search_results");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            
            // Repository tabs
            DrawRepositoryTabs();
            
            EditorGUILayout.Space();

            // List of packages
            _packagesScrollPosition = EditorGUILayout.BeginScrollView(_packagesScrollPosition);
            
            // Filter packages based on selected repository or upgrades
            List<Tuple<VPMRepository, VPMPackage, VPMPackageVersion>> packagesToShow = _filteredPackages;
            
            if (_selectedRepositoryId == "upgrades")
            {
                // Filter to show only packages with updates
                packagesToShow = _filteredPackages.Where(p => {
                    string installedVersion = VPMManifestManager.GetInstalledVersion(p.Item2.Id);
                    if (string.IsNullOrEmpty(installedVersion))
                        return false;
                    
                    VPMPackageVersion newerVersion = p.Item2.GetLatestNewerVersion(installedVersion, _showUnstableReleases);
                    return newerVersion != null;
                }).ToList();
            }
            else if (_selectedRepositoryId != "all")
            {
                packagesToShow = _filteredPackages.Where(p => p.Item1.Id == _selectedRepositoryId).ToList();
            }
            
            if (packagesToShow.Count == 0)
            {
                if (_filteredPackages.Count == 0)
                {
                    DrawFunMessage(_cachedFunMessages["no_packages"], new Color(0.8f, 0.4f, 0.2f)); // Orange color
                }
                else if (_selectedRepositoryId == "upgrades")
                {
                    DrawFunMessage(_cachedFunMessages["no_upgrades"]);
                }
                else if (!string.IsNullOrEmpty(_searchFilter))
                {
                    DrawFunMessage(_cachedFunMessages["no_search_results"], new Color(0.4f, 0.4f, 0.8f)); // Blue color
                }
                else
                {
                    DrawFunMessage(_cachedFunMessages["no_packages"], new Color(0.8f, 0.4f, 0.2f)); // Orange color
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
            
            // Check if this package is installed and get its version
            string installedVersion = VPMManifestManager.GetInstalledVersion(package.Id);
            bool isInstalled = !string.IsNullOrEmpty(installedVersion);
            
            // Check if there's a newer version available
            VPMPackageVersion newerVersion = null;
            if (isInstalled)
            {
                newerVersion = package.GetLatestNewerVersion(installedVersion, _showUnstableReleases);
            }
            
            // Initialize foldout state if not exists
            if (!_packageFoldouts.ContainsKey(packageKey))
            {
                _packageFoldouts[packageKey] = false;
            }

            // Save the current background color
            Color originalColor = GUI.backgroundColor;
            
            // Set a light green background for packages with updates
            if (isInstalled && newerVersion != null)
            {
                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f, 0.2f);
            }
            
            // Start the package box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Package header with foldout
            EditorGUILayout.BeginHorizontal();
            _packageFoldouts[packageKey] = EditorGUILayout.Foldout(_packageFoldouts[packageKey], package.GetDisplayName(), true);
            
            EditorGUI.BeginDisabledGroup(_isInstallingPackage);
            
            // Create a list of available versions
            List<string> versionOptions = package.Versions.Keys.ToList();
            
            // Filter out unstable versions if not showing unstable releases
            if (!_showUnstableReleases)
            {
                versionOptions = versionOptions.Where(v => VPMPackage.IsStableVersion(v)).ToList();
            }
            
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
            
            // If no versions are available after filtering, skip this package
            if (versionOptions.Count == 0)
            {
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                GUI.backgroundColor = originalColor; // Restore color before returning
                return;
            }
            
            // Find the index of the currently selected version
            int selectedIndex = versionOptions.IndexOf(selectedVersionString);
            if (selectedIndex < 0) selectedIndex = 0; // Default to first version if not found
            
            // Calculate the width needed for the version dropdown
            float versionWidth = EditorStyles.popup.CalcSize(new GUIContent(selectedVersionString)).x;
            
            // Draw the dropdown with calculated width
            int newSelectedIndex = EditorGUILayout.Popup(selectedIndex, versionOptions.ToArray(), GUILayout.Width(versionWidth));
            
            // Update selected version if changed
            if (newSelectedIndex != selectedIndex)
            {
                _selectedVersions[packageKey] = versionOptions[newSelectedIndex];
                // Update the selected version object
                selectedVersionString = versionOptions[newSelectedIndex];
                selectedVersion = package.Versions[selectedVersionString];
            }
            
            // Show appropriate button based on package state
            if (isInstalled)
            {
                string buttonText;
                try
                {
                    Version currentVer = new Version(installedVersion.Split('-')[0]);
                    Version selectedVer = new Version(selectedVersionString.Split('-')[0]);
                    
                    if (selectedVer > currentVer)
                    {
                        buttonText = "Upgrade";
                    }
                    else if (selectedVer < currentVer)
                    {
                        buttonText = "Downgrade";
                    }
                    else
                    {
                        buttonText = "Reinstall";
                    }
                }
                catch
                {
                    // If version parsing fails, default to reinstall
                    buttonText = "Reinstall";
                }
                
                // Calculate the width needed for the button
                float buttonWidth = EditorStyles.miniButton.CalcSize(new GUIContent(buttonText)).x;
                
                if (GUILayout.Button(buttonText, EditorStyles.miniButton, GUILayout.Width(buttonWidth)))
                {
                    InstallPackageAsync(selectedVersion);
                }
            }
            else
            {
                // Calculate the width needed for the install button
                float buttonWidth = EditorStyles.miniButton.CalcSize(new GUIContent("Install")).x;
                
                // Show install button if not installed
                if (GUILayout.Button("Install", EditorStyles.miniButton, GUILayout.Width(buttonWidth)))
                {
                    InstallPackageAsync(selectedVersion);
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            // Show package details if foldout is open
            if (_packageFoldouts[packageKey])
            {
                EditorGUILayout.Space();
                
                // Display package details
                // Check if this package appears in multiple repositories
                bool isInMultipleRepos = _repositories.Count(r => 
                    r.Packages != null && r.Packages.ContainsKey(package.Id)) > 1;
                
                EditorGUILayout.LabelField("Repository", isInMultipleRepos ? "Multiple Repositories" : (repository.Name ?? "Unknown Repository"));
                EditorGUILayout.LabelField("Version", selectedVersion.Version ?? "Unknown Version");
                EditorGUILayout.LabelField("Unity", selectedVersion.Unity ?? "Any Unity Version");
                
                // Show description and author from the selected version
                if (!string.IsNullOrEmpty(selectedVersion.Description))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
                    
                    // Create a style for the description text area
                    GUIStyle descriptionStyle = new GUIStyle(EditorStyles.textArea)
                    {
                        wordWrap = true,
                        richText = true,
                        stretchHeight = true,
                        normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : Color.black }
                    };
                    
                    // Calculate the height needed for the description
                    float descriptionHeight = descriptionStyle.CalcHeight(new GUIContent(selectedVersion.Description), EditorGUIUtility.currentViewWidth - 40);
                    
                    // Draw the description in a scrollable text area
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.SelectableLabel(selectedVersion.Description, descriptionStyle, GUILayout.Height(descriptionHeight));
                    EditorGUILayout.EndVertical();
                }
                
                if (!string.IsNullOrEmpty(selectedVersion.AuthorName))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Author", selectedVersion.AuthorName);
                }
                
                // Show update info if available
                if (isInstalled && newerVersion != null)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Update Available", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Current Version: {installedVersion}");
                    EditorGUILayout.LabelField($"New Version: {newerVersion.Version}");
                    
                    // Add a button to quickly update to the latest version
                    if (GUILayout.Button($"Update to {newerVersion.Version}", EditorStyles.miniButton))
                    {
                        InstallPackageAsync(newerVersion);
                    }
                }
            }
            
            // End the package box
            EditorGUILayout.EndVertical();
            
            // Restore the original background color
            GUI.backgroundColor = originalColor;
            
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
                
                // Refresh Unity's asset database for the specific package
                string packagePath = Path.Combine(Application.dataPath, "..", "Packages", packageVersion.Name);
                if (Directory.Exists(packagePath))
                {
                    // Force Unity to refresh the package directory
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    Debug.Log($"Refreshed Unity asset database for {packageVersion.Name}");
                }

                // Automatically sync the manifest after successful installation
                await ScanManifestAsync();
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
            int tabCount = _repositories.Count + 2; // +2 for "All Packages" and "Upgrades" tabs
            
            // Don't show tabs if there's only one repository
            if (tabCount <= 2)
            {
                return;
            }
            
            // Group repositories by their provider
            Dictionary<string, List<VPMRepository>> repositoryGroups = new Dictionary<string, List<VPMRepository>>();
            
            // Add repository groups
            repositoryGroups["VRC"] = new List<VPMRepository>();
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
                    repositoryGroups["VRC"].Add(repo);
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
            
            // "All" tab
            bool allSelected = GUILayout.Toggle(_selectedRepositoryId == "all" && _selectedRepositoryGroup == "none", 
                "All", EditorStyles.toolbarButton);
            if (allSelected && (_selectedRepositoryId != "all" || _selectedRepositoryGroup != "none"))
            {
                _selectedRepositoryId = "all";
                _selectedRepositoryGroup = "none";
            }
            
            // "Upgrades" tab
            bool upgradesSelected = GUILayout.Toggle(_selectedRepositoryId == "upgrades" && _selectedRepositoryGroup == "none", 
                "Upgrades", EditorStyles.toolbarButton);
            if (upgradesSelected && (_selectedRepositoryId != "upgrades" || _selectedRepositoryGroup != "none"))
            {
                _selectedRepositoryId = "upgrades";
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
                    
                    // Automatically select the first repository in the group
                    var sortedRepos = repositoryGroups[groupName].ToList();
                    if (_selectedRepositoryGroup == "VRC")
                    {
                        sortedRepos.Sort((a, b) => {
                            // Official repository should always be first
                            if (a.Id == "com.vrchat.repos.official") return -1;
                            if (b.Id == "com.vrchat.repos.official") return 1;
                            // Other repositories maintain their original order
                            return 0;
                        });
                    }
                    
                    if (sortedRepos.Count > 0)
                    {
                        _selectedRepositoryId = sortedRepos[0].Id;
                    }
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Second row - Subtabs for selected group
            if (_selectedRepositoryGroup != "none" && repositoryGroups.ContainsKey(_selectedRepositoryGroup))
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                
                // Draw subtabs for the selected group
                float currentLineWidth = 0; // Start with no indentation
                float windowWidth = EditorGUIUtility.currentViewWidth;
                
                // Sort repositories to ensure Official appears first in VRChat group
                var sortedRepos = repositoryGroups[_selectedRepositoryGroup].ToList();
                if (_selectedRepositoryGroup == "VRC")
                {
                    sortedRepos.Sort((a, b) => {
                        // Official repository should always be first
                        if (a.Id == "com.vrchat.repos.official") return -1;
                        if (b.Id == "com.vrchat.repos.official") return 1;
                        // Other repositories maintain their original order
                        return 0;
                    });
                }
                
                foreach (VPMRepository repo in sortedRepos)
                {
                    string subtabLabel = GetSubtabLabel(repo);
                    float buttonWidth = EditorStyles.toolbarButton.CalcSize(new GUIContent(subtabLabel)).x;
                    
                    // If adding this button would exceed the window width, start a new line
                    if (currentLineWidth + buttonWidth > windowWidth - 20) // 20px margin
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                        currentLineWidth = 0;
                    }
                    
                    // Draw the subtab
                    bool selected = GUILayout.Toggle(_selectedRepositoryId == repo.Id, 
                        subtabLabel, EditorStyles.toolbarButton);
                        
                    if (selected && _selectedRepositoryId != repo.Id)
                    {
                        _selectedRepositoryId = repo.Id;
                    }
                    
                    currentLineWidth += buttonWidth;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Gets the label to display for a repository subtab
        /// </summary>
        private string GetSubtabLabel(VPMRepository repo)
        {
            // Special labels for VRChat repositories
            if (repo.Id == "com.vrchat.repos.official")
            {
                return "Official";
            }
            else if (repo.Id == "com.vrchat.repos.curated")
            {
                return "Curated";
            }
            
            // For other repositories, use their name or ID
            return !string.IsNullOrEmpty(repo.Name) ? repo.Name : repo.Id;
        }
        
        private void RefreshPackages()
        {
            _filteredPackages.Clear();

            // First, collect all packages and track which ones appear in multiple repositories
            Dictionary<string, List<Tuple<VPMRepository, VPMPackage, VPMPackageVersion>>> packageOccurrences = 
                new Dictionary<string, List<Tuple<VPMRepository, VPMPackage, VPMPackageVersion>>>();

            foreach (VPMRepository repository in _repositories)
            {
                if (repository.Packages != null)
                {
                    foreach (var packagePair in repository.Packages)
                    {
                        VPMPackage package = packagePair.Value;
                        
                        // Get the latest version based on the unstable setting
                        VPMPackageVersion latestVersion = package.GetLatestVersion(_showUnstableReleases);

                        // Skip if no suitable version was found
                        if (latestVersion == null)
                            continue;

                        // Double-check version stability if unstable releases are disabled
                        if (!_showUnstableReleases && !VPMPackage.IsStableVersion(latestVersion.Version))
                            continue;

                        // Apply search filter if any
                        if (string.IsNullOrEmpty(_searchFilter) ||
                            latestVersion.DisplayName?.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            package.Id?.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            latestVersion.Description?.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var packageTuple = new Tuple<VPMRepository, VPMPackage, VPMPackageVersion>(repository, package, latestVersion);
                            
                            if (!packageOccurrences.ContainsKey(package.Id))
                            {
                                packageOccurrences[package.Id] = new List<Tuple<VPMRepository, VPMPackage, VPMPackageVersion>>();
                            }
                            packageOccurrences[package.Id].Add(packageTuple);
                        }
                    }
                }
            }

            // Now process the packages, handling duplicates
            foreach (var packageGroup in packageOccurrences)
            {
                string packageId = packageGroup.Key;
                var occurrences = packageGroup.Value;

                if (occurrences.Count == 1)
                {
                    // Single occurrence, add as is
                    _filteredPackages.Add(occurrences[0]);
                }
                else
                {
                    // Multiple occurrences, find the one with the latest version
                    var latestVersion = occurrences.OrderByDescending(p => 
                    {
                        try
                        {
                            return new Version(p.Item3.Version.Split('-')[0]);
                        }
                        catch
                        {
                            return new Version(0, 0, 0);
                        }
                    }).First();

                    // Add the latest version with a special repository
                    _filteredPackages.Add(latestVersion);
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
            
            // Initialize foldout state if not exists
            if (!_packageFoldouts.ContainsKey(package.name))
            {
                _packageFoldouts[package.name] = false;
            }
            
            _packageFoldouts[package.name] = EditorGUILayout.Foldout(_packageFoldouts[package.name], displayName, true);
            
            // Check if the package can be removed
            string disabledReason;
            bool canRemove = CanRemovePackage(package.name, out disabledReason);
            
            EditorGUI.BeginDisabledGroup(_isRemovingPackage || !canRemove);
            if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Remove Package", $"Are you sure you want to remove {displayName}?", "Yes", "No"))
                {
                    RemovePackageAsync(package);
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            
            if (_packageFoldouts[package.name])
            {
                EditorGUILayout.Space(5);
                
                // Show warning if package cannot be removed
                if (!canRemove)
                {
                    EditorGUILayout.HelpBox(disabledReason, MessageType.Warning);
                    EditorGUILayout.Space(5);
                }
                
                EditorGUILayout.LabelField("Package ID", package.name);
                EditorGUILayout.LabelField("Version", package.version);
                
                // Show dependencies if any
                if (_packageDependencies.TryGetValue(package.name, out var dependencies) && dependencies.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Dependencies", EditorStyles.boldLabel);
                    foreach (var dep in dependencies)
                    {
                        EditorGUILayout.LabelField("• " + dep);
                    }
                }
                
                if (!string.IsNullOrEmpty(package.description))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
                    
                    // Create a style for the description text area
                    GUIStyle descriptionStyle = new GUIStyle(EditorStyles.textArea)
                    {
                        wordWrap = true,
                        richText = true,
                        stretchHeight = true,
                        normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : Color.black }
                    };
                    
                    // Calculate the height needed for the description
                    float descriptionHeight = descriptionStyle.CalcHeight(new GUIContent(package.description), EditorGUIUtility.currentViewWidth - 40);
                    
                    // Draw the description in a scrollable text area
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.SelectableLabel(package.description, descriptionStyle, GUILayout.Height(descriptionHeight));
                    EditorGUILayout.EndVertical();
                }
                
                if (!string.IsNullOrEmpty(package.author))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Author", package.author);
                }
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
            
            // Check if the package can be removed
            string disabledReason;
            if (!CanRemovePackage(package.name, out disabledReason))
            {
                EditorUtility.DisplayDialog("Cannot Remove Package", disabledReason, "OK");
                return;
            }
                
            _isRemovingPackage = true;
            _currentRemovingPackage = !string.IsNullOrEmpty(package.displayName) ? package.displayName : package.name;
            
            bool success = false;
            await Task.Run(() => {
                try
                {
                    // Use the VPMManager to remove the package
                    VPMManager.RemovePackage(package.name);
                    Debug.Log($"Successfully removed package: {package.name}");
                    success = true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to remove package {package.name}: {e.Message}");
                    EditorUtility.DisplayDialog("Error", $"Failed to remove package {package.name}. Check the console for details.", "OK");
                }
            });
            
            _isRemovingPackage = false;
            
            // Refresh the list of installed packages and dependencies
            RefreshInstalledPackages();
            RefreshPackageDependencies();

            if (success)
            {
                // Automatically sync the manifest after successful removal
                await ScanManifestAsync();
            }
            
            Repaint();
        }

        private async Task ScanManifestAsync()
        {
            try
            {
                // Show progress dialog
                EditorUtility.DisplayProgressBar("Syncing Manifest", "Scanning installed packages...", 0f);

                // Run the scan in a background thread
                int updatedCount = await Task.Run(() => VPMManifestManager.ScanAndUpdateManifest());

                // Hide progress dialog
                EditorUtility.ClearProgressBar();

                // Show results only if there were updates
                if (updatedCount > 0)
                {
                    Debug.Log($"Manifest sync complete: Updated {updatedCount} package(s) in the manifest file.");
                }

                // Refresh the installed packages list
                RefreshInstalledPackages();
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"Error syncing manifest: {e.Message}");
            }
        }

        private void RefreshPackageDependencies()
        {
            _packageDependencies.Clear();
            _reverseDependencies.Clear();

            // Read the manifest file
            string manifestPath = Path.Combine(Application.dataPath, "..", "vpm-manifest.json");
            if (!File.Exists(manifestPath))
                return;

            try
            {
                string json = File.ReadAllText(manifestPath);
                JObject manifest = JObject.Parse(json);

                // Get the locked section which contains all dependencies
                JObject locked = manifest["locked"] as JObject;
                if (locked == null)
                    return;

                foreach (var package in locked)
                {
                    string packageName = package.Key;
                    JObject packageData = package.Value as JObject;
                    if (packageData == null)
                        continue;

                    // Get dependencies for this package
                    JObject dependencies = packageData["dependencies"] as JObject;
                    if (dependencies == null)
                        continue;

                    // Initialize the dependency sets if they don't exist
                    if (!_packageDependencies.ContainsKey(packageName))
                        _packageDependencies[packageName] = new HashSet<string>();
                    if (!_reverseDependencies.ContainsKey(packageName))
                        _reverseDependencies[packageName] = new HashSet<string>();

                    // Add each dependency
                    foreach (var dependency in dependencies)
                    {
                        string depName = dependency.Key;
                        string depVersion = dependency.Value.ToString();

                        // Add to package's dependencies
                        if (!_packageDependencies.ContainsKey(packageName))
                            _packageDependencies[packageName] = new HashSet<string>();
                        _packageDependencies[packageName].Add(depName);

                        // Add to reverse dependencies
                        if (!_reverseDependencies.ContainsKey(depName))
                            _reverseDependencies[depName] = new HashSet<string>();
                        _reverseDependencies[depName].Add(packageName);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing manifest file: {e.Message}");
            }
        }

        private bool CanRemovePackage(string packageName, out string reason)
        {
            reason = null;

            // Check if this package is a dependency of any other installed package
            if (_reverseDependencies.TryGetValue(packageName, out var dependents) && dependents.Count > 0)
            {
                var dependentNames = string.Join(", ", dependents);
                reason = $"Cannot remove {packageName} because it is required by: {dependentNames}";
                return false;
            }

            // Special case for VRChat base package
            if (packageName == "com.vrchat.base")
            {
                bool avatarsInstalled = _installedPackages.Any(p => p.name == "com.vrchat.avatars");
                bool worldsInstalled = _installedPackages.Any(p => p.name == "com.vrchat.worlds");
                
                if (avatarsInstalled || worldsInstalled)
                {
                    reason = $"Cannot remove VRChat Base while {(avatarsInstalled ? "Avatars" : "Worlds")} SDK is installed";
                    return false;
                }
            }
            // Prevent Redline Package Manager from removing itself
            else if (packageName == "dev.redline-team.rpm")
            {
                reason = "Cannot remove Redline Package Manager (RPM) as it cannot uninstall itself";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Draws a fun message with custom styling
        /// </summary>
        private void DrawFunMessage(string message, Color? textColor = null)
        {
            // Create a custom style for the fun message
            GUIStyle funStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = textColor ?? new Color(0.2f, 0.8f, 0.2f) } // Default to green if no color specified
            };
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(message, funStyle, GUILayout.Height(40));
            EditorGUILayout.Space(10);
        }

        /// <summary>
        /// Gets a random pair of ASCII decorations
        /// </summary>
        private (string left, string right) GetRandomDecoration()
        {
            (string, string)[] decorations = new[] {
                ("*** ", " ***"),
                ("=== ", " ==="),
                (">>> ", " <<<"),
                ("~~~ ", " ~~~"),
                ("[ ", " ]"),
                ("< ", " >"),
                ("{ ", " }"),
                ("( ", " )")
            };
            return decorations[UnityEngine.Random.Range(0, decorations.Length)];
        }

        /// <summary>
        /// Gets a random fun message for different UI states
        /// </summary>
        private string GetFunMessage(string messageType)
        {
            // Get random decoration pair
            var decoration = GetRandomDecoration();

            switch (messageType)
            {
                case "no_upgrades":
                    string[] upgradeMessages = new[] {
                        "Everything's up to date! Time for a coffee break ☕",
                        "All caught up! Your packages are living their best life",
                        "No upgrades needed! Your setup is looking fresh",
                        "Everything's current! You're ahead of the curve",
                        "All packages are up to date! You're crushing it",
                        "No upgrades available! Your packages are already in the future",
                        "Everything's perfect! Time to celebrate!",
                        "No upgrades needed! Your packages are already at peak performance"
                    };
                    return decoration.left + upgradeMessages[UnityEngine.Random.Range(0, upgradeMessages.Length)] + decoration.right;

                case "no_packages":
                    string[] noPackagesMessages = new[] {
                        "No packages found! Time to go shopping!",
                        "Nothing here yet! Ready to discover some cool packages?",
                        "Package shelf is empty! Let's fill it up",
                        "No packages in the library! Time to add some",
                        "Package collection is empty! Let's build it up",
                        "No packages yet! Ready to start your collection?",
                        "Package list is empty! Time to add some goodies",
                        "No packages found! Ready to launch your collection?"
                    };
                    return decoration.left + noPackagesMessages[UnityEngine.Random.Range(0, noPackagesMessages.Length)] + decoration.right;

                case "no_search_results":
                    string[] searchMessages = new[] {
                        "No matches found! Try different keywords",
                        "Search came up empty! Time to try something else",
                        "Nothing matches your search! Let's try again",
                        "Search results are empty! Maybe try different terms?",
                        "No packages match your search! Time to explore",
                        "Search found nothing! Let's try another approach",
                        "No matches! Ready to try a different search?",
                        "Search returned empty! Let's try something else"
                    };
                    return decoration.left + searchMessages[UnityEngine.Random.Range(0, searchMessages.Length)] + decoration.right;

                case "no_repositories":
                    string[] repoMessages = new[] {
                        "No repositories yet! Time to add some",
                        "Repository shelf is empty! Let's fill it up",
                        "No repositories found! Ready to discover some?",
                        "Repository list is empty! Time to add some",
                        "No repositories yet! Let's build your collection",
                        "Repository collection is empty! Ready to start?",
                        "No repositories found! Time to add some",
                        "Repository list is empty! Let's launch your collection"
                    };
                    return decoration.left + repoMessages[UnityEngine.Random.Range(0, repoMessages.Length)] + decoration.right;

                default:
                    return "No items found";
            }
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
