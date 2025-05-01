/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * Date: 2023-6-21
 */
using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Configuration
{
    /// <summary>
    /// Represents a local applet configuration section
    /// </summary>
    [XmlType(nameof(AppletConfigurationSection), Namespace = "http://santedb.org/configuration")]
    public class AppletConfigurationSection : IConfigurationSection
    {

        /// <summary>
        /// Applet configuration section
        /// </summary>
        public AppletConfigurationSection()
        {
            this.AppletDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? Assembly.GetExecutingAssembly().Location), "applets");
            this.DefaultApplet = "org.santedb.uicore";
            this.DefaultSolution = "santedb.core.sln";

#if DEBUG
            this.AllowUnsignedApplets = true;
#endif
        }

        /// <summary>
        /// Gets or sets the directory for applets to be loaded from
        /// </summary>
        [XmlAttribute("appletDirectory")]
        [DisplayName("Applet Directory")]
        [Description("Identifies the directory location where applets should be loaded")]
        [Editor("System.Windows.Forms.Design.FolderNameEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
        public String AppletDirectory { get; set; }


        /// <summary>
        /// For deployments with multiple solutions on the server, this specifies the default solution.
        /// </summary>
        [XmlElement("defaultSolution"), JsonProperty("defaultSolution"), DisplayName("Default Solution"), Description("For SanteDB deployments which have multiple solutions, this specifies the solution which serve as the base for web pages. When not set the default system applet is used")]
        public string DefaultSolution { get; set; }

        /// <summary>
        /// Gets or sets the startup asset
        /// </summary>
        [XmlElement("defaultApplet"), JsonProperty("defaultApplet"), DisplayName("Default Asset"), Description("Normally, asset paths in the package are rendered via /app-id/page.html however for requests on the root the default needs to be specified (default is org.santedb.uicore)")]
        public string DefaultApplet { get; set; }

        /// <summary>
        /// Allow unsigned applets to be installed
        /// </summary>
        [XmlAttribute("allowUnsignedApplets")]
        [DisplayName("Allow Unsigned Code")]
        [Description("Allows unsigned applets to be installed and executed on this server (NOT RECOMMENDED)")]
        public bool AllowUnsignedApplets { get; set; }

        /// <summary>
        /// Trusted publishers
        /// </summary>
        [XmlArray("trustedPublishers"), XmlArrayItem("add")]
        [DisplayName("Trusted Publishers")]
        [Description("Identifies the thumbprints of software publishers that are treated as TRUSTED in this environment")]
        [Obsolete("Trusted Publishers can no longer be configured via command line - certificates must be installed into host trust store", true)]
        public List<string> TrustedPublishers { get; set; }

    }
}
