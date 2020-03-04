/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: fyfej
 * Date: 2019-11-27
 */
using SanteDB.Core.Applets.Model;
using System;
using System.Collections.Generic;

namespace SanteDB.Core.Applets.Services
{
    /// <summary>
    /// Represents a service which loads/registers applet solutions
    /// </summary>
    public interface IAppletSolutionManagerService
    {
        /// <summary>
        /// Get the solutions configured on the server
        /// </summary>
        IEnumerable<AppletSolution> Solutions { get; }

        /// <summary>
        /// Get only applets in the specified solution
        /// </summary>
        /// <param name="solutionId"></param>
        /// <returns></returns>
        ReadonlyAppletCollection GetApplets(String solutionId);

        /// <summary>
        /// Uninstall a package
        /// </summary>
        bool UnInstall(String solutionId);

        /// <summary>
        /// Installs or upgrades an existing applet collection via package
        /// </summary>
        bool Install(AppletSolution solution, bool isUpgrade = false);

        /// <summary>
        /// Get the specified applet manifest
        /// </summary>
        AppletManifest GetApplet(String solutionId, String appletId);

        /// <summary>
        /// Gets the installed applet package source for the specified applet
        /// </summary>
        byte[] GetPackage(String solutionId, String appletId);

    }
}
