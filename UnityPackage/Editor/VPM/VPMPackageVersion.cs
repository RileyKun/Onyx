using System;
using UnityEngine;

namespace Redline.Editor.VPM
{
    /// <summary>
    /// Represents a specific version of a package
    /// </summary>
    [Serializable]
    public class VPMPackageVersion
    {
        /// <summary>
        /// The version string (e.g. "1.2.3" or "1.2.3-beta.1")
        /// </summary>
        public string Version;

        /// <summary>
        /// The display name of the package
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// The name of the package
        /// </summary>
        public string Name;

        /// <summary>
        /// The Unity version required for this package
        /// </summary>
        public string Unity;

        /// <summary>
        /// The description of the package
        /// </summary>
        public string Description;

        /// <summary>
        /// The author's name
        /// </summary>
        public string AuthorName;

        /// <summary>
        /// The author's URL
        /// </summary>
        public string AuthorUrl;

        /// <summary>
        /// The license of the package
        /// </summary>
        public string License;

        /// <summary>
        /// The URL to the changelog
        /// </summary>
        public string ChangelogUrl;

        /// <summary>
        /// The SHA256 hash of the zip file
        /// </summary>
        public string ZipSHA256;

        /// <summary>
        /// The URL to download the package
        /// </summary>
        public string Url;

        /// <summary>
        /// The package this version belongs to
        /// </summary>
        [NonSerialized]
        public VPMPackage Package;
    }
} 