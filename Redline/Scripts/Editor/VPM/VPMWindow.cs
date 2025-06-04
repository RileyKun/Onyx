using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Redline.Scripts.Editor;

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

        private enum PackagesSubTab
        {
            Available,
            History
        }
        
        // Banner style
        private GUIStyle _bannerStyle;

        private Tab _currentTab = Tab.Repositories;
        private PackagesSubTab _currentPackagesSubTab = PackagesSubTab.Available;
        private Vector2 _repositoriesScrollPosition;
        private Vector2 _packagesScrollPosition;
        private Vector2 _installedScrollPosition;
        private string _newRepositoryUrl = "";
        private bool _isAddingRepository = false;
        private float _installProgress = 0f;
        private bool _isInstallingPackage = false;
        private string _currentInstallingPackage = "";
        private string _searchFilter = "";
        private string _authorFilter = "";
        private string _versionFilter = "";
        private bool _showOnlyInstalled = false;
        private bool _showOnlyUpdatable = false;
        private bool _showAdvancedFilters = false;
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
        private bool _showImportOptions = false; // Whether to show the import options section
        
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

        // Installation history
        private class InstallationRecord
        {
            public string PackageName;
            public string Version;
            public DateTime Timestamp;
            public bool WasSuccessful;
            public string ErrorMessage;
        }
        private List<InstallationRecord> _installationHistory = new List<InstallationRecord>();
        private Vector2 _historyScrollPosition;
        private bool _showFailedOnly = false;

        // Add new cache fields
        private List<Tuple<VPMRepository, VPMPackage, VPMPackageVersion>> _cachedFilteredPackages;
        private string _lastSearchFilter;
        private string _lastAuthorFilter;
        private string _lastVersionFilter;
        private bool _lastShowOnlyInstalled;
        private bool _lastShowOnlyUpdatable;
        private bool _lastShowUnstableReleases;
        private string _lastSelectedRepositoryId;
        private bool _needsPackageRefresh = true;

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
                }
            };

            // Generate initial fun messages
            GenerateFunMessages();

            // Generate initial fun messages
            GenerateFunMessages();
            LoadInstallationHistory();

            // Initialize cache
            _cachedFilteredPackages = null;
            _lastSearchFilter = _searchFilter;
            _lastAuthorFilter = _authorFilter;
            _lastVersionFilter = _versionFilter;
            _lastShowOnlyInstalled = _showOnlyInstalled;
            _lastShowOnlyUpdatable = _showOnlyUpdatable;
            _lastShowUnstableReleases = _showUnstableReleases;
            _lastSelectedRepositoryId = _selectedRepositoryId;
            _needsPackageRefresh = false;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                _needsPackageRefresh = true;
                RefreshPackages();
            }
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

        private void OnGUI()
        {
            // Wrap everything in a try-catch to prevent editor crashes
            try
            {
                // Check if window was resized
                if (Event.current.type == EventType.Layout)
                {
                    _needsPackageRefresh = true;
                }

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
            catch (System.Exception e)
            {
                // Log the error but don't crash the editor
                Debug.LogError($"Error in VPM Window: {e.Message}\n{e.StackTrace}");
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

            // Sync VPM-Manifest versions
            if (GUILayout.Button("Sync Manifest", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                ScanManifestAsync();
            }

            // Add settings button
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Settings").image, "Open Redline Settings"), 
                EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                RedlineSettings.Init();
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
            if (GUILayout.Button("Add", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                AddRepositoryAsync(_newRepositoryUrl);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            
            // Add import repositories foldout
            EditorGUILayout.Space(5);
            _showImportOptions = EditorGUILayout.Foldout(_showImportOptions, "Import Repositories", true);

            if (_showImportOptions)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // VCC/ALCOM import section
                EditorGUILayout.LabelField("From VCC/ALCOM", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Import repositories from your VRChat Creator Companion or ALCOM installation.", MessageType.Info);
                
                EditorGUI.BeginDisabledGroup(_isAddingRepository);
                if (GUILayout.Button("Import from VCC/ALCOM", EditorStyles.miniButton, GUILayout.Height(30)))
                {
                    ImportVCCRepositoriesAsync();
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(10);

                // Default repositories section
                EditorGUILayout.LabelField("Default Repositories", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Import the default repositories that come with RPM.", MessageType.Info);
                
                EditorGUI.BeginDisabledGroup(_isAddingRepository);
                if (GUILayout.Button("Import Default Repositories", EditorStyles.miniButton, GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Import Default Repositories",
                        "This will import the default repositories to your Redline directory. " +
                        "Any existing repositories with the same name will be overwritten. Continue?",
                        "Import", "Cancel")) {
                        VPMRepository.CopyDefaultRepositories(true);
                        RefreshRepositories();
                        RefreshPackages();
                    }
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndVertical();
            }
            
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

            // Draw packages subtabs
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Toggle(_currentPackagesSubTab == PackagesSubTab.Available, "Available", EditorStyles.toolbarButton))
            {
                _currentPackagesSubTab = PackagesSubTab.Available;
            }
            if (GUILayout.Toggle(_currentPackagesSubTab == PackagesSubTab.History, "History", EditorStyles.toolbarButton))
            {
                _currentPackagesSubTab = PackagesSubTab.History;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            switch (_currentPackagesSubTab)
            {
                case PackagesSubTab.Available:
                    DrawAvailablePackages();
                    break;
                case PackagesSubTab.History:
                    DrawHistoryTab();
                    break;
            }
        }

        private void DrawAvailablePackages()
        {
            // Advanced search filters
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _showAdvancedFilters = EditorGUILayout.Foldout(_showAdvancedFilters, "Advanced Filters", true);
            
            if (_showAdvancedFilters)
            {
                EditorGUI.indentLevel++;
                
                // Basic search
                string newSearchFilter = EditorGUILayout.TextField("Search", _searchFilter);
                if (newSearchFilter != _searchFilter)
                {
                    _searchFilter = newSearchFilter;
                    RefreshPackages();
                }

                // Author filter
                string newAuthorFilter = EditorGUILayout.TextField("Author", _authorFilter);
                if (newAuthorFilter != _authorFilter)
                {
                    _authorFilter = newAuthorFilter;
                    RefreshPackages();
                }

                // Version filter
                string newVersionFilter = EditorGUILayout.TextField("Version", _versionFilter);
                if (newVersionFilter != _versionFilter)
                {
                    _versionFilter = newVersionFilter;
                    RefreshPackages();
                }

                // Additional filters
                bool newShowOnlyInstalled = EditorGUILayout.Toggle("Show Only Installed", _showOnlyInstalled);
                if (newShowOnlyInstalled != _showOnlyInstalled)
                {
                    _showOnlyInstalled = newShowOnlyInstalled;
                    RefreshPackages();
                }

                bool newShowOnlyUpdatable = EditorGUILayout.Toggle("Show Only Updatable", _showOnlyUpdatable);
                if (newShowOnlyUpdatable != _showOnlyUpdatable)
                {
                    _showOnlyUpdatable = newShowOnlyUpdatable;
                    RefreshPackages();
                }

                EditorGUI.indentLevel--;
            }
            else
            {
                // Simple search when advanced filters are collapsed
                string newSearchFilter = EditorGUILayout.TextField("Search", _searchFilter);
                if (newSearchFilter != _searchFilter)
                {
                    _searchFilter = newSearchFilter;
                    RefreshPackages();
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            
            // Repository tabs
            DrawRepositoryTabs();
            
            EditorGUILayout.Space();

            // Filter packages based on selected repository or upgrades
            List<Tuple<VPMRepository, VPMPackage, VPMPackageVersion>> packagesToShow = _filteredPackages;
            
            if (_selectedRepositoryId == "upgrades")
            {
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

            // Begin scroll view
            _packagesScrollPosition = EditorGUILayout.BeginScrollView(_packagesScrollPosition);
            
            if (packagesToShow.Count == 0)
            {
                if (_filteredPackages.Count == 0)
                {
                    DrawFunMessage(_cachedFunMessages["no_packages"], new Color(0.8f, 0.4f, 0.2f));
                }
                else if (_selectedRepositoryId == "upgrades")
                {
                    DrawFunMessage(_cachedFunMessages["no_upgrades"]);
                }
                else if (!string.IsNullOrEmpty(_searchFilter))
                {
                    DrawFunMessage(_cachedFunMessages["no_search_results"], new Color(0.4f, 0.4f, 0.8f));
                }
                else
                {
                    DrawFunMessage(_cachedFunMessages["no_packages"], new Color(0.8f, 0.4f, 0.2f));
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
            
            GUILayout.FlexibleSpace();
            
            // Unstable releases toggle (right side)
            bool newShowUnstable = EditorGUILayout.Toggle("Show Unstable Releases", _showUnstableReleases);
            if (newShowUnstable != _showUnstableReleases)
            {
                _showUnstableReleases = newShowUnstable;
                RefreshPackages();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Debug options section
            if (_showDebugOptions)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Debug Settings", EditorStyles.boldLabel);
                
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
                _selectedVersions[packageKey] = latestVersion.Version;
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
                    Version vA = new Version(a.Split('-')[0]);
                    Version vB = new Version(b.Split('-')[0]);
                    return vB.CompareTo(vA);
                }
                catch {
                    return string.Compare(b, a);
                }
            });
            
            // If no versions are available after filtering, skip this package
            if (versionOptions.Count == 0)
            {
                GUI.backgroundColor = originalColor;
                return;
            }
            
            // Find the index of the currently selected version
            int selectedIndex = versionOptions.IndexOf(selectedVersionString);
            if (selectedIndex < 0) selectedIndex = 0;
            
            // Calculate the width needed for the version dropdown
            float versionWidth = EditorStyles.popup.CalcSize(new GUIContent(selectedVersionString)).x;
            
            // Start the package box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Package header with foldout
            EditorGUILayout.BeginHorizontal();
            _packageFoldouts[packageKey] = EditorGUILayout.Foldout(_packageFoldouts[packageKey], package.GetDisplayName(), true);
            
            EditorGUI.BeginDisabledGroup(_isInstallingPackage);
            
            // Draw the dropdown with calculated width
            int newSelectedIndex = EditorGUILayout.Popup(selectedIndex, versionOptions.ToArray(), GUILayout.Width(versionWidth));
            
            // Update selected version if changed
            if (newSelectedIndex != selectedIndex)
            {
                _selectedVersions[packageKey] = versionOptions[newSelectedIndex];
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
                    buttonText = "Reinstall";
                }
                
                float buttonWidth = EditorStyles.miniButton.CalcSize(new GUIContent(buttonText)).x;
                
                if (GUILayout.Button(buttonText, EditorStyles.miniButton, GUILayout.Width(buttonWidth)))
                {
                    InstallPackageAsync(selectedVersion);
                }
            }
            else
            {
                float buttonWidth = EditorStyles.miniButton.CalcSize(new GUIContent("Install")).x;
                
                if (GUILayout.Button("Install", EditorStyles.miniButton, GUILayout.Width(buttonWidth)))
                {
                    InstallPackageAsync(selectedVersion);
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            // Only show package details if foldout is open
            if (_packageFoldouts[packageKey])
            {
                EditorGUILayout.Space();
                
                // Display package details
                bool isInMultipleRepos = _repositories.Count(r => 
                    r.Packages != null && r.Packages.ContainsKey(package.Id)) > 1;
                
                EditorGUILayout.LabelField("Repository", isInMultipleRepos ? "Multiple Repositories" : (repository.Name ?? "Unknown Repository"));
                EditorGUILayout.LabelField("Version", selectedVersion.Version ?? "Unknown Version");
                EditorGUILayout.LabelField("Unity", selectedVersion.Unity ?? "Any Unity Version");
                
                if (package.Versions.Count > 1)
                {
                    EditorGUILayout.Space(5);
                    if (GUILayout.Button("Compare Versions", EditorStyles.miniButton))
                    {
                        ShowVersionComparison(package);
                    }
                }

                if (!string.IsNullOrEmpty(selectedVersion.Description))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
                    
                    GUIStyle descriptionStyle = new GUIStyle(EditorStyles.textArea)
                    {
                        wordWrap = true,
                        richText = true,
                        stretchHeight = true,
                        normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : Color.black }
                    };
                    
                    float descriptionHeight = descriptionStyle.CalcHeight(new GUIContent(selectedVersion.Description), EditorGUIUtility.currentViewWidth - 40);
                    EditorGUILayout.SelectableLabel(selectedVersion.Description, descriptionStyle, GUILayout.Height(descriptionHeight));
                }
                
                if (!string.IsNullOrEmpty(selectedVersion.AuthorName))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Author", selectedVersion.AuthorName);
                }
                
                if (isInstalled && newerVersion != null)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Update Available", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Current Version: {installedVersion}");
                    EditorGUILayout.LabelField($"New Version: {newerVersion.Version}");
                    
                    if (GUILayout.Button($"Update to {newerVersion.Version}", EditorStyles.miniButton))
                    {
                        InstallPackageAsync(newerVersion);
                    }
                }
            }
            
            EditorGUILayout.EndVertical();
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

            // Add to installation history
            AddInstallationRecord(
                packageVersion.Name,
                packageVersion.Version,
                success,
                success ? null : "Installation failed. Check the console for details."
            );

            // Always refresh after installation completes, regardless of success
            VPMManager.LoadAllRepositories();
            RefreshRepositories();
            MarkPackagesForRefresh();
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
            // Check if we need to refresh based on filter changes
            bool filtersChanged = _lastSearchFilter != _searchFilter ||
                                _lastAuthorFilter != _authorFilter ||
                                _lastVersionFilter != _versionFilter ||
                                _lastShowOnlyInstalled != _showOnlyInstalled ||
                                _lastShowOnlyUpdatable != _showOnlyUpdatable ||
                                _lastShowUnstableReleases != _showUnstableReleases ||
                                _lastSelectedRepositoryId != _selectedRepositoryId ||
                                _needsPackageRefresh;

            if (!filtersChanged && _cachedFilteredPackages != null)
            {
                _filteredPackages = _cachedFilteredPackages;
                return;
            }

            _filteredPackages.Clear();

            // First, collect all packages and track which ones appear in multiple repositories
            Dictionary<string, List<Tuple<VPMRepository, VPMPackage, VPMPackageVersion>>> packageOccurrences = 
                new Dictionary<string, List<Tuple<VPMRepository, VPMPackage, VPMPackageVersion>>>();

            foreach (VPMRepository repository in _repositories)
            {
                if (repository.Packages == null) continue;

                foreach (var packagePair in repository.Packages)
                {
                    VPMPackage package = packagePair.Value;
                    
                    // Get the latest version based on the unstable setting
                    VPMPackageVersion latestVersion = package.GetLatestVersion(_showUnstableReleases);
                    if (latestVersion == null) continue;

                    // Skip if unstable releases are disabled and version is unstable
                    if (!_showUnstableReleases && !VPMPackage.IsStableVersion(latestVersion.Version))
                        continue;

                    // Apply all filters
                    bool matchesFilters = true;

                    // Basic search filter
                    if (!string.IsNullOrEmpty(_searchFilter))
                    {
                        matchesFilters &= (
                            (latestVersion.DisplayName?.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                            (package.Id?.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                            (latestVersion.Description?.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                        );
                    }

                    // Author filter
                    if (!string.IsNullOrEmpty(_authorFilter))
                    {
                        matchesFilters &= (latestVersion.AuthorName?.IndexOf(_authorFilter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
                    }

                    // Version filter
                    if (!string.IsNullOrEmpty(_versionFilter))
                    {
                        matchesFilters &= (latestVersion.Version?.IndexOf(_versionFilter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
                    }

                    // Installed filter
                    if (_showOnlyInstalled)
                    {
                        matchesFilters &= VPMManager.IsPackageInstalled(package.Id);
                    }

                    // Updatable filter
                    if (_showOnlyUpdatable)
                    {
                        string installedVersion = VPMManifestManager.GetInstalledVersion(package.Id);
                        matchesFilters &= !string.IsNullOrEmpty(installedVersion) && 
                                        package.GetLatestNewerVersion(installedVersion, _showUnstableReleases) != null;
                    }

                    if (matchesFilters)
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

            // Process packages and handle duplicates
            foreach (var packageGroup in packageOccurrences)
            {
                var occurrences = packageGroup.Value;
                if (occurrences.Count == 1)
                {
                    _filteredPackages.Add(occurrences[0]);
                }
                else
                {
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

                    _filteredPackages.Add(latestVersion);
                }
            }

            // Sort packages by display name
            _filteredPackages = _filteredPackages.OrderBy(p => p.Item2.GetDisplayNameWithVersion()).ToList();

            // Cache the results
            _cachedFilteredPackages = _filteredPackages;
            _lastSearchFilter = _searchFilter;
            _lastAuthorFilter = _authorFilter;
            _lastVersionFilter = _versionFilter;
            _lastShowOnlyInstalled = _showOnlyInstalled;
            _lastShowOnlyUpdatable = _showOnlyUpdatable;
            _lastShowUnstableReleases = _showUnstableReleases;
            _lastSelectedRepositoryId = _selectedRepositoryId;
            _needsPackageRefresh = false;
        }
        
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
                    
                // Skip VRCFury's temp package
                if (dirName == "com.vrcfury.temp")
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
            if (GUILayout.Button("Refresh List", EditorStyles.miniButton, GUILayout.Width(100)))
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

            // Add restore from backup button
            if (GUILayout.Button("Restore", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Restore Package", 
                    $"Are you sure you want to restore {displayName} from the most recent backup?", "Yes", "No"))
                {
                    RestorePackageFromBackupAsync(package);
                }
            }

            // Add dependency visualization button
            if (GUILayout.Button("Dependencies", EditorStyles.miniButton, GUILayout.Width(100)))
            {
                ShowDependencyGraph(package);
            }
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
                        // Try to get the installed version of the dependency
                        string depVersion = VPMManifestManager.GetInstalledVersion(dep);
                        string depText = dep;
                        if (!string.IsNullOrEmpty(depVersion))
                        {
                            depText += $" (v{depVersion})";
                        }
                        EditorGUILayout.LabelField("• " + depText);
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

            // Special handling for VRCFury removal
            if (package.name == "com.vrchat.vrcfury")
            {
                bool removeTempFolder = EditorUtility.DisplayDialog(
                    "Remove VRCFury",
                    "Would you like to also remove VRCFury's temporary folder (com.vrcfury.temp)?\n\n" +
                    "This folder contains cached data that can be safely removed and will be recreated when needed.",
                    "Yes, Remove Temp Folder",
                    "No, Keep Temp Folder"
                );

                if (removeTempFolder)
                {
                    string tempPackagePath = Path.Combine(VPMManager.GetPackagesDirectory(), "com.vrcfury.temp");
                    if (Directory.Exists(tempPackagePath))
                    {
                        try
                        {
                            Directory.Delete(tempPackagePath, true);
                            Debug.Log("Removed VRCFury's temporary folder");
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Failed to remove VRCFury's temporary folder: {e.Message}");
                        }
                    }
                }
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
            MarkPackagesForRefresh();
            RefreshPackages();

            if (success)
            {
                // Automatically sync the manifest after successful removal
                await ScanManifestAsync();
            }
            
            Repaint();
        }

        private async void RestorePackageFromBackupAsync(InstalledPackageInfo package)
        {
            _isRemovingPackage = true;
            _currentRemovingPackage = !string.IsNullOrEmpty(package.displayName) ? package.displayName : package.name;
            
            bool success = await Task.Run(() => VPMManager.RestorePackageFromBackup(package.name));
            
            _isRemovingPackage = false;
            
            if (success)
            {
                // Refresh the list of installed packages
                RefreshInstalledPackages();
                // Automatically sync the manifest after successful restoration
                await ScanManifestAsync();
                EditorUtility.DisplayDialog("Restore Complete", 
                    $"Successfully restored {package.displayName} from backup.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Restore Failed", 
                    $"Failed to restore {package.displayName} from backup. Check the console for details.", "OK");
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

            string projectPath = Directory.GetParent(Application.dataPath).FullName;

            // Read the vpm-manifest.json
            string manifestPath = Path.Combine(projectPath, "vpm-manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(manifestPath);
                    JObject manifest = JObject.Parse(json);

                    // Get the locked section which contains all dependencies
                    JObject locked = manifest["locked"] as JObject;
                    if (locked != null)
                    {
                        foreach (var package in locked)
                        {
                            string packageName = package.Key;
                            JObject packageData = package.Value as JObject;
                            if (packageData == null)
                                continue;

                            // Get dependencies for this package
                            JObject dependencies = packageData["dependencies"] as JObject;
                            if (dependencies != null)
                            {
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

                                    // Skip Unity packages
                                    if (depName.StartsWith("com.unity."))
                                        continue;

                                    // Add to package's dependencies
                                    _packageDependencies[packageName].Add(depName);

                                    // Add to reverse dependencies
                                    if (!_reverseDependencies.ContainsKey(depName))
                                        _reverseDependencies[depName] = new HashSet<string>();
                                    _reverseDependencies[depName].Add(packageName);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error parsing vpm-manifest.json: {e.Message}");
                }
            }

            // Read the package-lock.json
            string packageLockPath = Path.Combine(projectPath, "package-lock.json");
            if (File.Exists(packageLockPath))
            {
                try
                {
                    string json = File.ReadAllText(packageLockPath);
                    JObject packageLock = JObject.Parse(json);

                    // Get the packages section
                    JObject packages = packageLock["packages"] as JObject;
                    if (packages != null)
                    {
                        foreach (var package in packages)
                        {
                            string packageName = package.Key;
                            JObject packageData = package.Value as JObject;
                            if (packageData == null)
                                continue;

                            // Get dependencies for this package
                            JObject dependencies = packageData["dependencies"] as JObject;
                            if (dependencies != null)
                            {
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

                                    // Skip Unity packages
                                    if (depName.StartsWith("com.unity."))
                                        continue;

                                    // Add to package's dependencies
                                    _packageDependencies[packageName].Add(depName);

                                    // Add to reverse dependencies
                                    if (!_reverseDependencies.ContainsKey(depName))
                                        _reverseDependencies[depName] = new HashSet<string>();
                                    _reverseDependencies[depName].Add(packageName);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error parsing package-lock.json: {e.Message}");
                }
            }

            // Now scan all installed packages for additional dependencies
            string packagesDir = Path.Combine(projectPath, "Packages");
            if (Directory.Exists(packagesDir))
            {
                foreach (string packageDir in Directory.GetDirectories(packagesDir))
                {
                    string packageJsonPath = Path.Combine(packageDir, "package.json");
                    if (File.Exists(packageJsonPath))
                    {
                        try
                        {
                            string packageJson = File.ReadAllText(packageJsonPath);
                            JObject packageData = JObject.Parse(packageJson);
                            
                            string packageName = packageData["name"]?.ToString();
                            if (string.IsNullOrEmpty(packageName))
                                continue;

                            // Get dependencies from package.json
                            JObject dependencies = packageData["dependencies"] as JObject;
                            if (dependencies != null)
                            {
                                // Initialize dependency sets if they don't exist
                                if (!_packageDependencies.ContainsKey(packageName))
                                    _packageDependencies[packageName] = new HashSet<string>();
                                if (!_reverseDependencies.ContainsKey(packageName))
                                    _reverseDependencies[packageName] = new HashSet<string>();

                                // Add each dependency
                                foreach (var dependency in dependencies)
                                {
                                    string depName = dependency.Key;
                                    string depVersion = dependency.Value.ToString();

                                    // Skip Unity packages
                                    if (depName.StartsWith("com.unity."))
                                        continue;

                                    // Add to package's dependencies
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
                            Debug.LogError($"Error parsing package.json in {packageDir}: {e.Message}");
                        }
                    }
                }
            }

            // Add implicit dependencies for VRChat packages
            AddImplicitVRChatDependencies();
        }

        private void AddImplicitVRChatDependencies()
        {
            // Add implicit dependencies for VRChat packages
            string[] vrcPackages = new[] { "com.vrchat.avatars", "com.vrchat.worlds" };
            string basePackage = "com.vrchat.base";

            foreach (string vrcPackage in vrcPackages)
            {
                if (_packageDependencies.ContainsKey(vrcPackage))
                {
                    _packageDependencies[vrcPackage].Add(basePackage);
                    
                    if (!_reverseDependencies.ContainsKey(basePackage))
                        _reverseDependencies[basePackage] = new HashSet<string>();
                    _reverseDependencies[basePackage].Add(vrcPackage);
                }
            }

            // Add implicit dependencies for VRCFury
            string vrcfuryPackage = "com.vrchat.vrcfury";
            if (_packageDependencies.ContainsKey(vrcfuryPackage))
            {
                // VRCFury implicitly depends on VRChat SDK
                _packageDependencies[vrcfuryPackage].Add("com.vrchat.avatars");
                _packageDependencies[vrcfuryPackage].Add("com.vrchat.base");
                
                if (!_reverseDependencies.ContainsKey("com.vrchat.avatars"))
                    _reverseDependencies["com.vrchat.avatars"] = new HashSet<string>();
                _reverseDependencies["com.vrchat.avatars"].Add(vrcfuryPackage);
                
                if (!_reverseDependencies.ContainsKey("com.vrchat.base"))
                    _reverseDependencies["com.vrchat.base"] = new HashSet<string>();
                _reverseDependencies["com.vrchat.base"].Add(vrcfuryPackage);
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

        private void ShowDependencyGraph(InstalledPackageInfo package)
        {
            // Create a new window for the dependency graph
            var window = GetWindow<PackageDependencyWindow>("Package Dependencies");
            window.Initialize(package, _packageDependencies, _reverseDependencies);
            window.Show();
        }

        private void DrawHistoryTab()
        {
            EditorGUILayout.LabelField("Installation History", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Filter options
            EditorGUILayout.BeginHorizontal();
            _showFailedOnly = EditorGUILayout.ToggleLeft("Show Failed Only", _showFailedOnly);
            if (GUILayout.Button("Clear History", EditorStyles.miniButton, GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("Clear History", 
                    "Are you sure you want to clear the installation history?", "Yes", "No"))
                {
                    _installationHistory.Clear();
                    SaveInstallationHistory();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // History list
            _historyScrollPosition = EditorGUILayout.BeginScrollView(_historyScrollPosition);

            var filteredHistory = _showFailedOnly 
                ? _installationHistory.Where(r => !r.WasSuccessful)
                : _installationHistory;

            if (!filteredHistory.Any())
            {
                EditorGUILayout.HelpBox(
                    _showFailedOnly ? "No failed installations found" : "No installation history found",
                    MessageType.Info);
            }
            else
            {
                foreach (var record in filteredHistory.OrderByDescending(r => r.Timestamp))
                {
                    DrawHistoryItem(record);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHistoryItem(InstallationRecord record)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Status icon and package info
            EditorGUILayout.BeginHorizontal();
            GUIStyle statusStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = record.WasSuccessful ? Color.green : Color.red }
            };
            EditorGUILayout.LabelField(record.WasSuccessful ? "✓" : "✗", statusStyle, GUILayout.Width(20));
            EditorGUILayout.LabelField($"{record.PackageName} v{record.Version}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            // Timestamp
            EditorGUILayout.LabelField($"Installed: {record.Timestamp:g}", EditorStyles.miniLabel);

            // Error message if failed
            if (!record.WasSuccessful && !string.IsNullOrEmpty(record.ErrorMessage))
            {
                EditorGUILayout.HelpBox(record.ErrorMessage, MessageType.Error);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void AddInstallationRecord(string packageName, string version, bool success, string errorMessage = null)
        {
            _installationHistory.Add(new InstallationRecord
            {
                PackageName = packageName,
                Version = version,
                Timestamp = DateTime.Now,
                WasSuccessful = success,
                ErrorMessage = errorMessage
            });

            // Keep only the last 100 records
            if (_installationHistory.Count > 100)
            {
                _installationHistory = _installationHistory.OrderByDescending(r => r.Timestamp)
                    .Take(100)
                    .ToList();
            }

            SaveInstallationHistory();
        }

        private void SaveInstallationHistory()
        {
            try
            {
                string historyPath = Path.Combine(Application.dataPath, "..", "Packages", "VPM_History.json");
                string json = JsonUtility.ToJson(new { History = _installationHistory }, true);
                File.WriteAllText(historyPath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save installation history: {e.Message}");
            }
        }

        private void LoadInstallationHistory()
        {
            try
            {
                string historyPath = Path.Combine(Application.dataPath, "..", "Packages", "VPM_History.json");
                if (File.Exists(historyPath))
                {
                    string json = File.ReadAllText(historyPath);
                    var wrapper = JsonUtility.FromJson<HistoryWrapper>(json);
                    _installationHistory = wrapper.History;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load installation history: {e.Message}");
                _installationHistory = new List<InstallationRecord>();
            }
        }

        [Serializable]
        private class HistoryWrapper
        {
            public List<InstallationRecord> History = new List<InstallationRecord>();
        }

        private void ShowVersionComparison(VPMPackage package)
        {
            var window = GetWindow<PackageComparisonWindow>("Version Comparison");
            window.Initialize(package);
            window.Show();
        }

        // Add method to mark packages for refresh
        private void MarkPackagesForRefresh()
        {
            _needsPackageRefresh = true;
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

    /// <summary>
    /// Window for visualizing package dependencies
    /// </summary>
    public class PackageDependencyWindow : EditorWindow
    {
        private InstalledPackageInfo _package;
        private Dictionary<string, HashSet<string>> _dependencies;
        private Dictionary<string, HashSet<string>> _reverseDependencies;
        private Vector2 _scrollPosition;
        private Dictionary<string, bool> _packageFoldouts = new Dictionary<string, bool>();

        public void Initialize(InstalledPackageInfo package, 
            Dictionary<string, HashSet<string>> dependencies,
            Dictionary<string, HashSet<string>> reverseDependencies)
        {
            _package = package;
            _dependencies = dependencies;
            _reverseDependencies = reverseDependencies;
            titleContent = new GUIContent($"Dependencies: {package.displayName}");
        }

        private void OnGUI()
        {
            if (_package == null)
                return;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Dependency Graph for {_package.displayName}", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Show direct dependencies
            EditorGUILayout.LabelField("Direct Dependencies", EditorStyles.boldLabel);
            if (_dependencies.TryGetValue(_package.name, out var deps) && deps.Count > 0)
            {
                foreach (var dep in deps)
                {
                    DrawDependencyItem(dep, "→");
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No direct dependencies", MessageType.Info);
            }

            EditorGUILayout.Space(10);

            // Show reverse dependencies (packages that depend on this one)
            EditorGUILayout.LabelField("Dependent Packages", EditorStyles.boldLabel);
            if (_reverseDependencies.TryGetValue(_package.name, out var revDeps) && revDeps.Count > 0)
            {
                foreach (var dep in revDeps)
                {
                    DrawDependencyItem(dep, "←");
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No packages depend on this one", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDependencyItem(string packageName, string arrow)
        {
            if (!_packageFoldouts.ContainsKey(packageName))
            {
                _packageFoldouts[packageName] = false;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(arrow, GUILayout.Width(20));
            _packageFoldouts[packageName] = EditorGUILayout.Foldout(_packageFoldouts[packageName], packageName, true);
            EditorGUILayout.EndHorizontal();

            if (_packageFoldouts[packageName])
            {
                EditorGUI.indentLevel++;
                
                // Show nested dependencies
                if (_dependencies.TryGetValue(packageName, out var nestedDeps) && nestedDeps.Count > 0)
                {
                    EditorGUILayout.LabelField("Dependencies:", EditorStyles.miniLabel);
                    foreach (var nestedDep in nestedDeps)
                    {
                        EditorGUILayout.LabelField($"  • {nestedDep}", EditorStyles.miniLabel);
                    }
                }

                // Show nested reverse dependencies
                if (_reverseDependencies.TryGetValue(packageName, out var nestedRevDeps) && nestedRevDeps.Count > 0)
                {
                    EditorGUILayout.LabelField("Dependent Packages:", EditorStyles.miniLabel);
                    foreach (var nestedRevDep in nestedRevDeps)
                    {
                        EditorGUILayout.LabelField($"  • {nestedRevDep}", EditorStyles.miniLabel);
                    }
                }

                EditorGUI.indentLevel--;
            }
        }
    }

    /// <summary>
    /// Window for comparing different versions of a package
    /// </summary>
    public class PackageComparisonWindow : EditorWindow
    {
        private VPMPackage _package;
        private Vector2 _scrollPosition;
        private string _selectedVersion1;
        private string _selectedVersion2;
        private Dictionary<string, bool> _sectionFoldouts = new Dictionary<string, bool>();

        public void Initialize(VPMPackage package)
        {
            _package = package;
            titleContent = new GUIContent($"Compare: {package.GetDisplayName()}");

            // Select the two most recent versions by default
            var sortedVersions = package.Versions.Keys
                .OrderByDescending(v => new Version(v.Split('-')[0]))
                .ToList();

            if (sortedVersions.Count >= 2)
            {
                _selectedVersion1 = sortedVersions[0];
                _selectedVersion2 = sortedVersions[1];
            }
            else if (sortedVersions.Count == 1)
            {
                _selectedVersion1 = sortedVersions[0];
                _selectedVersion2 = sortedVersions[0];
            }
        }

        private void OnGUI()
        {
            if (_package == null)
                return;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Compare Versions of {_package.GetDisplayName()}", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Version selection
            EditorGUILayout.BeginHorizontal();
            
            // Version 1 dropdown
            EditorGUILayout.LabelField("Version 1:", GUILayout.Width(60));
            int index1 = Array.IndexOf(_package.Versions.Keys.ToArray(), _selectedVersion1);
            int newIndex1 = EditorGUILayout.Popup(index1, _package.Versions.Keys.ToArray());
            if (newIndex1 != index1)
            {
                _selectedVersion1 = _package.Versions.Keys.ToArray()[newIndex1];
            }

            // Version 2 dropdown
            EditorGUILayout.LabelField("Version 2:", GUILayout.Width(60));
            int index2 = Array.IndexOf(_package.Versions.Keys.ToArray(), _selectedVersion2);
            int newIndex2 = EditorGUILayout.Popup(index2, _package.Versions.Keys.ToArray());
            if (newIndex2 != index2)
            {
                _selectedVersion2 = _package.Versions.Keys.ToArray()[newIndex2];
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Comparison view
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var version1 = _package.Versions[_selectedVersion1];
            var version2 = _package.Versions[_selectedVersion2];

            // Compare each field
            CompareField("Display Name", version1.DisplayName, version2.DisplayName);
            CompareField("Unity Version", version1.Unity, version2.Unity);
            CompareField("Description", version1.Description, version2.Description);
            CompareField("Author", version1.AuthorName, version2.AuthorName);
            CompareField("License", version1.License, version2.License);
            CompareField("Changelog URL", version1.ChangelogUrl, version2.ChangelogUrl);

            EditorGUILayout.EndScrollView();
        }

        private void CompareField(string fieldName, string value1, string value2)
        {
            if (!_sectionFoldouts.ContainsKey(fieldName))
            {
                _sectionFoldouts[fieldName] = true;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Header with foldout
            EditorGUILayout.BeginHorizontal();
            _sectionFoldouts[fieldName] = EditorGUILayout.Foldout(_sectionFoldouts[fieldName], fieldName, true);
            
            // Show change indicator if values are different
            if (value1 != value2)
            {
                GUIStyle changeStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = Color.yellow }
                };
                EditorGUILayout.LabelField("Changed", changeStyle);
            }
            EditorGUILayout.EndHorizontal();

            if (_sectionFoldouts[fieldName])
            {
                EditorGUI.indentLevel++;

                // Version 1 value
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Version 1:", GUILayout.Width(60));
                EditorGUILayout.LabelField(value1 ?? "N/A");
                EditorGUILayout.EndHorizontal();

                // Version 2 value
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Version 2:", GUILayout.Width(60));
                EditorGUILayout.LabelField(value2 ?? "N/A");
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
}
