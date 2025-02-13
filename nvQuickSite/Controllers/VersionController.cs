﻿// Copyright (c) 2016-2020 nvisionative, Inc.
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
    using System.Net;

    using nvQuickNop.Controllers.Exceptions;
    using Serilog;

    /// <summary>
    /// Manages versions.
    /// </summary>
    public static class VersionController
    {
        /// <summary>
        /// Gets the latest nvQuickSite version available on Github.
        /// </summary>
        /// <returns>The latest version as a string.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Security",
            "CA5386:Avoid hardcoding SecurityProtocolType value",
            Justification = "Workaround since Github enforces Tls12 but we don't know the user config.")]
        public static string GetRemoteLatestVersion()
        {
            using (WebClient client = new WebClient())
            {
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    var url = "https://raw.githubusercontent.com/nvisionative/nvQuickSite/master/nvQuickSite/data/latestVersion.json";
                    string result = client.DownloadString(url);
                    Models.Version res = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.Version>(result);
                    Log.Logger.Debug("v{version} available on GitHub.", res.latestVersion);
                    return res.latestVersion;
                }
                catch (WebException ex)
                {
                    var message = "There was an error reading the latest version of nvQuickSite. Please\ncheck your internet connection.";
                    Log.Logger.Error(ex, message.Replace("\n", " "));
                    throw new VersionControllerException(message, ex) { Source = "Get Remote Latest Version" };
                }
            }
        }
    }
}
