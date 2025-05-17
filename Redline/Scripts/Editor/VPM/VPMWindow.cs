using System;
using System.Collections.Generic;
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
            Packages
        }
        
        // Banner style
        private GUIStyle _bannerStyle;

        private Tab _currentTab = Tab.Repositories;
        private Vector2 _repositoriesScrollPosition;
        private Vector2 _packagesScrollPosition;
        private string _newRepositoryUrl = "";
        private bool _isAddingRepository = false;
        private float _installProgress = 0f;
        private bool _isInstallingPackage = false;
        private string _currentInstallingPackage = "";
        private string _searchFilter = "";
        private Dictionary<string, bool> _repositoryFoldouts = new Dictionary<string, bool>();
        
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
            
            // Initialize the banner style
            _bannerStyle = new GUIStyle
            {
                normal = {
                    background = Resources.Load("RedlinePMHeader") as Texture2D,
                    textColor = Color.white
                },
                fixedHeight = 100 // Half the height of the info window banner
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
        }

        private void DrawToolbar()
        {
            // Draw the Redline banner at the top
            if (_bannerStyle != null && _bannerStyle.normal.background != null)
            {
                GUILayout.Box("", _bannerStyle);
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

        private void DrawPackageItem(VPMRepository repository, VPMPackage package, VPMPackageVersion version)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(package.GetDisplayName(), EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(_isInstallingPackage);
            if (GUILayout.Button("Install", GUILayout.Width(60)))
            {
                InstallPackageAsync(version);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Repository", repository.Name ?? "Unknown Repository");
            EditorGUILayout.LabelField("Version", version.Version ?? "Unknown Version");
            EditorGUILayout.LabelField("Unity", version.Unity ?? "Any Unity Version");
            
            if (!string.IsNullOrEmpty(version.Description))
            {
                EditorGUILayout.LabelField("Description", version.Description);
            }
            
            if (!string.IsNullOrEmpty(version.AuthorName))
            {
                EditorGUILayout.LabelField("Author", version.AuthorName);
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

            if (success)
            {
                EditorUtility.DisplayDialog("Success", $"{_currentInstallingPackage} has been installed successfully.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", $"Failed to install {_currentInstallingPackage}. See console for details.", "OK");
            }
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
        
        /// <summary>
        /// Refreshes all repositories from their URLs asynchronously
        /// </summary>
        private async void RefreshRepositoriesFromUrlsAsync()
        {
            if (_isRefreshingRepositories)
                return;
                
            _isRefreshingRepositories = true;
            Repaint();
            
            try
            {
                int refreshedCount = await VPMManager.RefreshRepositoriesFromUrls();
                RefreshRepositories();
                RefreshPackages();
                
                if (refreshedCount > 0)
                {
                    EditorUtility.DisplayDialog("Repositories Refreshed", 
                        $"Successfully refreshed {refreshedCount} repositories with the latest packages.", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("No Updates", 
                        "No repositories were updated. Either there were no updates available or the repositories don't have URLs for refreshing.", "OK");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error refreshing repositories: {e.Message}");
                EditorUtility.DisplayDialog("Error", 
                    "An error occurred while refreshing repositories. See console for details.", "OK");
            }
            finally
            {
                _isRefreshingRepositories = false;
                Repaint();
            }
        }
    }
}
