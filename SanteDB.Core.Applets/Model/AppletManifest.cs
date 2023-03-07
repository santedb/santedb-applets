/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2022-5-30
 */
using SanteDB.Core.Model.Serialization;
using SharpCompress.Compressors.LZMA;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Model
{
    /// <summary>
    /// The applet manifest is responsible for storing data related to a JavaScript applet
    /// </summary>
    [XmlType(nameof(AppletManifest), Namespace = "http://santedb.org/applet")]
    [XmlRoot(nameof(AppletManifest), Namespace = "http://santedb.org/applet")]
    [ExcludeFromCodeCoverage]
    public class AppletManifest : IEquatable<AppletManifest>
    {

        /// <summary>
        /// Applet configuration setting
        /// </summary>
        [XmlType(nameof(AppletConfigurationSettings), Namespace = "http://santedb.org/applet")]
        [Obsolete]
        public class AppletConfigurationSettings
        {
            [XmlArray("appSettings"), XmlArrayItem("add")]
            public List<AppletSettingEntry> Settings { get; set; }
        }

        private static XmlSerializer x_xsz = XmlModelSerializerFactory.Current.CreateSerializer(typeof(AppletManifest));

        /// <summary>
        /// Load the specified manifest name
        /// </summary>
        public static AppletManifest Load(Stream resourceStream)
        {

            var amfst = x_xsz.Deserialize(resourceStream) as AppletManifest;
            amfst.Initialize();
            return amfst;
        }

        /// <summary>
        /// Save this applet manifest to the stream
        /// </summary>
        public void Save(Stream resourceStream)
        {
            x_xsz.Serialize(resourceStream, this);
        }

        /// <summary>
        /// Initialize the applet manifest
        /// </summary>
        public void Initialize()
        {
            foreach (var ast in this.Assets)
            {
                ast.Manifest = this;
            }

            foreach (var mnu in this.Menus)
            {
                mnu.Initialize(this);
            }
        }

        /// <summary>
        /// Create an unsigned package
        /// </summary>
        /// <returns>The package.</returns>
        public AppletPackage CreatePackage()
        {
            AppletPackage retVal = new AppletPackage()
            {
                Meta = this.Info
            };
            this.Info.TimeStamp = DateTime.Now;
            using (var ms = new MemoryStream())
            {
                using (var ls = new LZipStream(ms, SharpCompress.Compressors.CompressionMode.Compress))
                {
                    x_xsz.Serialize(ls, this);
                }
                retVal.Manifest = ms.ToArray();
            }
            return retVal;
        }

        /// <summary>
        /// Applet information itself
        /// </summary>
        [XmlElement("info")]
        public AppletInfo Info
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the locales in the applet
        /// </summary>
        [XmlArray("locales")]
        [XmlArrayItem("locale")]
        public List<AppletLocale> Locales { get; set; }

        /// <summary>
        /// Instructs the host which asset can be used as a starting point
        /// </summary>
        [XmlElement("startupAsset")]
        public String StartAsset { get; set; }


        /// <summary>
        /// Instructs the host which asset can be used as a starting point
        /// </summary>
        [XmlElement("loginAsset")]
        public String LoginAsset { get; set; }

        /// <summary>
        /// Instructs the host which asset can be used as a starting point
        /// </summary>
        [XmlArray("errors"), XmlArrayItem("add")]
        public List<AppletErrorAssetDefinition> ErrorAssets { get; set; }

        /// <summary>
        /// Attempt to retrieve the assets <paramref name="assetPath"/>
        /// </summary>
        /// <param name="assetPath">The path to the asset name in this collection</param>
        /// <param name="asset">The resolved asset</param>
        /// <returns>True if the asset was successfully retrieved</returns>
        public bool TryGetAsset(string assetPath, out AppletAsset asset)
        {
            asset = this.Assets.Find(o => o.Name == assetPath);
            return asset != null;
        }

        /// <summary>
        /// Initial applet configuration
        /// </summary>
        /// <value>The configuration.</value>
        [XmlArray("settings"), XmlArrayItem("add")]
        public List<AppletSettingEntry> Settings
        {
            get;
            set;
        }

        /// <summary>
        /// Applet configuration settings legacy
        /// </summary>
        [XmlElement("configuration")]
        public AppletConfigurationSettings ConfigurationSettingsObsolete {
            get => null;
            set => this.Settings = value.Settings;
        }

        /// <summary>
        /// Gets or sets the tile sizes the applet can have
        /// </summary>
        [XmlElement("menuItem")]
        public List<AppletMenu> Menus
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or ets the templates for use in the applet
        /// </summary>
        [XmlElement("template")]
        public List<AppletTemplateDefinition> Templates { get; set; }

        /// <summary>
        /// Gets or sets the view model definitions 
        /// </summary>
        [XmlElement("viewModel")]
        public List<AppletViewModelDefinition> ViewModel { get; set; }

        /// <summary>
        /// Gets or sets the assets which are to be used in the applet
        /// </summary>
        [XmlElement("asset")]
        public List<AppletAsset> Assets
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the applet strings
        /// </summary>
        [XmlElement("strings")]
        public List<AppletStrings> Strings { get; set; }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
        public bool Equals(AppletManifest other)
        {
            if (other == null)
            {
                return false;
            }

            if (this.Info == null && other.Info == null)
            {
                return true;
            }

            return this.Info?.Id == other.Info?.Id;
        }

        /// <summary>
        /// Add a setting to the applet
        /// </summary>
        /// <param name="name">The name of the setting</param>
        /// <param name="value">The value of the setting</param>
        public void AddSetting(string name, string value)
        {
            if(this.Settings == null)
            {
                this.Settings = new List<AppletSettingEntry>();
            }
            var existingSetting = this.Settings.Find(o => o.Name == name);
            if(existingSetting != null)
            {
                existingSetting.Value = value;
            }
            else
            {
                this.Settings.Add(new AppletSettingEntry(name, value));
            }
        }

        /// <summary>
        /// Gets the specified applet setting
        /// </summary>
        /// <param name="name">The name of the setting</param>
        /// <returns>The setting</returns>
        public String GetSetting(string name) => this.Settings?.Find(o => o.Name == name)?.Value;
    }
}