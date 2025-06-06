﻿/*
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
using SanteDB.Core.Model.Serialization;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Model
{
    /// <summary>
    /// Applet package used for installations only
    /// </summary>
    [XmlType(nameof(AppletPackage), Namespace = "http://santedb.org/applet")]
    [XmlRoot(nameof(AppletPackage), Namespace = "http://santedb.org/applet")]
    [ExcludeFromCodeCoverage]
    public class AppletPackage
    {


        /// <summary>
        /// Applet package
        /// </summary>
        public AppletPackage()
        {
            this.Version = typeof(AppletPackage).Assembly.GetName().Version.ToString();
        }

        // Serializer
        private static XmlSerializer s_packageSerializer = XmlModelSerializerFactory.Current.CreateSerializer(typeof(AppletPackage));
        private static XmlSerializer s_solutionSerializer = XmlModelSerializerFactory.Current.CreateSerializer(typeof(AppletSolution));

        /// <summary>
        /// Load the specified manifest name
        /// </summary>
        public static AppletPackage Load(byte[] resourceData)
        {
            using (MemoryStream ms = new MemoryStream(resourceData))
            {
                return AppletPackage.Load(ms);
            }
        }

        /// <summary>
        /// Load the specified manifest name
        /// </summary>
        public static AppletPackage Load(Stream resourceStream)
        {
            using (GZipStream gzs = new GZipStream(resourceStream, CompressionMode.Decompress))
            {
                using (var xr = XmlReader.Create(gzs))
                {
                    AppletPackage retVal = null;
                    if (s_packageSerializer.CanDeserialize(xr))
                    {
                        retVal = s_packageSerializer.Deserialize(xr) as AppletPackage;
                    }
                    else if (s_solutionSerializer.CanDeserialize(xr))
                    {
                        retVal = s_solutionSerializer.Deserialize(xr) as AppletSolution;
                    }

                    return retVal;
                }
            }
        }

        /// <summary>
        /// Applet reference metadata
        /// </summary>
        [XmlElement("info"), JsonProperty("info")]
        public AppletInfo Meta
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or ses the manifest to be installed
        /// </summary>
        /// <value>The manifest.</value>
        [XmlElement("manifest"), JsonIgnore]
        public byte[] Manifest
        {
            get;
            set;
        }

        /// <summary>
        /// The pak version
        /// </summary>
        [XmlAttribute("pakVersion"), JsonIgnore]
        public String Version { get; set; }

        /// <summary>
        /// Public signing certificate
        /// </summary>
        [XmlElement("certificate"), JsonIgnore]
        public byte[] PublicKey { get; set; }

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
        /// Unpack the package
        /// </summary>
        public AppletManifest Unpack()
        {
            using (MemoryStream ms = new MemoryStream(this.Manifest))
            using (LZipStream gs = new LZipStream(NonDisposingStream.Create(ms), CompressionMode.Decompress))
            {
                var retVal = AppletManifest.Load(gs);
                if (this.PublicKey != null)
                {
                    retVal.PublisherCertificate = new X509Certificate2(this.PublicKey);
                }
                return retVal;
            }
        }

        /// <summary>
        /// Save the compressed applet manifest
        /// </summary>
        public void Save(Stream stream)
        {
            using (GZipStream gzs = new GZipStream(NonDisposingStream.Create(stream), CompressionMode.Compress))
            {
                if (this is AppletSolution)
                {
                    s_solutionSerializer.Serialize(gzs, this);
                }
                else
                {
                    s_packageSerializer.Serialize(gzs, this);
                }
            }
        }
    }
}

