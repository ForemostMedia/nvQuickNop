// Copyright (c) 2016-2020 nvisionative, Inc.
//
// This file is part of nvQuickSite.
//
// nvQuickSite is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// nvQuickSite is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with nvQuickSite.  If not, see <http://www.gnu.org/licenses/>.

namespace nvQuickNop.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;

    using nvQuickNop.Models;
    using Octokit;
    using Serilog;

    /// <summary>
    /// Manages packages.
    /// </summary>
    public static class PackageController
    {
        /// <summary>
        /// Gets a value indicating whether the system can access the GitHub repository.
        /// </summary>
        internal static bool IsOnline
        {
            get
            {
                bool canRead;
                try
                {
                    using (var client = new WebClient())
                    {
                        canRead = client.OpenRead("https://github.com/nvisionative/nvQuickSite").CanRead;
                        Log.Logger.Information("Internet appears to be " + (canRead ? "online" : "offline"));
                        return canRead;
                    }
                }
                catch (WebException ex)
                {
                    Log.Logger.Information("Internet appears offline");
                    Log.Logger.Error(ex, "Unexpected error occurred while checking internet access to GitHub repository");
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets the list of packages available.
        /// </summary>
        /// <returns>An enumeration of packages.</returns>
        public static IEnumerable<Package> GetPackageList()
        {
            var localPackages = GetLocalPackages();
            var packages = localPackages.ToList();
            if (PackageController.IsOnline)
            {

                var ghPackages = GetGitHubPackages();
                if (ghPackages.Any())
                {
                    packages = ghPackages.ToList();
                }
            }

            SaveLocalPackagesFile(packages);
            Log.Logger.Information("Saved local packages file");
            Log.Logger.Debug("Saved packages to local packages file: {@packages}", packages);
            return packages;
        }

        private static IEnumerable<Package> GetLocalPackages()
        {
            var localPackages = new List<Package>();
            var packagesFile = Directory.GetCurrentDirectory() + @"\Downloads\packages.nop.json";
            if (File.Exists(packagesFile))
            {
                using (var sr = new StreamReader(packagesFile))
                {
                    var content = sr.ReadToEnd();
                    localPackages = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Package>>(content);
                }
            }

            Log.Logger.Information("Loaded local packages");
            Log.Logger.Debug("Loaded local packages: {@localPackages}", localPackages);
            return localPackages;
        }

        private static void SaveLocalPackagesFile(IEnumerable<Package> packages)
        {
            var downloadDirectory = GetDownloadDirectory();
            if (!Directory.Exists(downloadDirectory))
            {
                Directory.CreateDirectory(downloadDirectory);
            }

            var pfile = Directory.GetCurrentDirectory() + @"\Downloads\packages.nop.json";
            using (var sw = new StreamWriter(pfile))
            {
                sw.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(packages, Newtonsoft.Json.Formatting.Indented));
            }
        }

        private static string GetDownloadDirectory()
        {
            return Directory.GetCurrentDirectory() + @"\Downloads\";
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "On purpose so any exception here does not prevent using local packages.")]
        private static IEnumerable<Package> GetGitHubPackages()
        {
            var packages = new List<Package>();
            try
            {
                var client = new GitHubClient(new ProductHeaderValue("nvQuickSite"));
                var releases = client.Repository.Release.GetAll("nopSolutions", "nopCommerce").Result;

                if (releases.Count > 0)
                {
                    var index = 0;
                    foreach (Release release in releases.Where(x => !x.Name.ToLower().Contains("beta")))
                    {
                        var installPackage = release.Assets
                            .Where(a =>
                                a.BrowserDownloadUrl.IndexOf("nosource_win_x64", StringComparison.OrdinalIgnoreCase) > -1 ||
                                a.BrowserDownloadUrl.IndexOf("_nosource.rar", StringComparison.OrdinalIgnoreCase) > -1)
                            .FirstOrDefault();

                        var upgradePackage = release.Assets
                            .Where(a =>
                                a.BrowserDownloadUrl.IndexOf("source", StringComparison.OrdinalIgnoreCase) > -1 &&
                                a.BrowserDownloadUrl.IndexOf("nopcommerce", StringComparison.OrdinalIgnoreCase) > -1)
                            .FirstOrDefault();

                        var ghPackage = new Package();

                        ghPackage.version = TrimTagName(release);

                        if (index == 0 &&
                            release.Name.IndexOf("rc", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            Properties.Settings.Default.ShowReleaseCandidates &&
                            installPackage != null)
                        {
                            ghPackage.did = "dnn-platform-rc";
                            ghPackage.name = "DNN Platform Release Candidate";
                            ghPackage.url = installPackage.BrowserDownloadUrl;
                            ghPackage.upgradeurl = upgradePackage.BrowserDownloadUrl;
                            packages.Add(ghPackage);
                        }
                        else if (!release.Name.ToUpperInvariant().Contains("RC") &&
                            installPackage != null)
                        {
                            ghPackage.did = "nopcommerce_" + ghPackage.version.Substring(0,4);
                            ghPackage.name = "nopCommerce " + ghPackage.version.Substring(0,4);
                            ghPackage.url = installPackage.BrowserDownloadUrl;
                            ghPackage.upgradeurl = upgradePackage.BrowserDownloadUrl;
                            packages.Add(ghPackage);
                        }

                        index++;
                    }
                }

                Log.Logger.Information("Retrieved NopCommerce packages from GitHub");
                Log.Logger.Debug("Retrieved NopCommerce packages from GitHub: {@packages}", packages);
                return packages;
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Unexpected error occurred retrieving NopCommerce packages from GitHub");
                return packages;
            }
        }

        private static string TrimTagName(Release release)
        {
            if (release.TagName != null && release.TagName.StartsWith("release-"))
            {
                return release.TagName.Substring(8);
            }
            else
            {
                return release.TagName;
            }
        }
    }
}
