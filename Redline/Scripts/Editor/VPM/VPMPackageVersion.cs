using System;

namespace Redline.Editor.VPM
{
    /// <summary>
    /// Represents a specific version of a VPM package
    /// </summary>
    [Serializable]
    public class VPMPackageVersion
    {
        /// <summary>
        /// The name of the package
        /// </summary>
        public string Name;
        
        /// <summary>
        /// The display name of the package
        /// </summary>
        public string DisplayName;
        
        /// <summary>
        /// The version string
        /// </summary>
        public string Version;
        
        /// <summary>
        /// The Unity version this package is compatible with
        /// </summary>
        public string Unity;
        
        /// <summary>
        /// Description of the package
        /// </summary>
        public string Description;
        
        /// <summary>
        /// URL to the changelog
        /// </summary>
        public string ChangelogUrl;
        
        /// <summary>
        /// Name of the author
        /// </summary>
        public string AuthorName;
        
        /// <summary>
        /// URL of the author
        /// </summary>
        public string AuthorUrl;
        
        /// <summary>
        /// The license of the package
        /// </summary>
        public string License;
        
        /// <summary>
        /// SHA256 hash of the zip file
        /// </summary>
        public string ZipSHA256;
        
        /// <summary>
        /// URL to download the package zip file
        /// </summary>
        public string Url;
    }
}
