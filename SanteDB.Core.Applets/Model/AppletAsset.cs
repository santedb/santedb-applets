/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 */
using SharpCompress.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Model
{

    /// <summary>
    /// Represents an applet asset
    /// </summary>
    [XmlType(nameof(AppletAsset), Namespace = "http://santedb.org/applet")]
    [ExcludeFromCodeCoverage]
    public class AppletAsset
    {

        // Decompressed content
        private object m_decompressedContent = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.Core.Applets.Model.AppletAsset"/> class.
        /// </summary>
        public AppletAsset()
        {
        }

        /// <summary>
        /// Gets the or sets the manifest to which the asset belongs
        /// </summary>
        [XmlIgnore]
        public AppletManifest Manifest { get; internal set; }

        /// <summary>
        /// Gets or sets the name of the asset
        /// </summary>
        [XmlAttribute("name")]
        public String Name
        {
            get;
            set;
        }

        /// <summary>
        /// Language
        /// </summary>
        [XmlAttribute("lang")]
        public String Language
        {
            get;
            set;
        }

        /// <summary>
        /// Mime type
        /// </summary>
        [XmlAttribute("mimeType")]
        public String MimeType
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the applets required policies for a user to run
        /// </summary>
        [XmlElement("demand")]
        public List<String> Policies
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the content of the asset
        /// </summary>
        /// <remarks>
        /// Assets of type contentXml 
        /// </remarks>
        [XmlElement("contentText", Type = typeof(String))]
        [XmlElement("contentBin", Type = typeof(byte[]))]
        [XmlElement("contentXml", Type = typeof(XElement))]
        [XmlElement("contentHtml", Type = typeof(AppletAssetHtml))]
        [XmlElement("widgetHtml", Type = typeof(AppletWidget))]
        [XmlElement("virtual", Type = typeof(AppletAssetVirtual))]
        public Object Content
        {
            get => this.m_decompressedContent;
            set
            {
                switch (value)
                {
                    case byte[] bytea:
                        // is the content compressed?
                        if (Encoding.UTF8.GetString(bytea, 0, 4) == "LZIP")
                        {
                            using (var ms = new MemoryStream(bytea))
                            using (var ls = new SharpCompress.Compressors.LZMA.LZipStream(NonDisposingStream.Create(ms), SharpCompress.Compressors.CompressionMode.Decompress))
                            using (var oms = new MemoryStream())
                            {
                                ls.CopyTo(oms);
                                this.m_decompressedContent = oms.ToArray();
                            }
                        }
                        else
                        {
                            this.m_decompressedContent = bytea;
                        }
                        break;
                    case XElement xe:
                        using (MemoryStream ms = new MemoryStream())
                        using (XmlWriter xw = XmlWriter.Create(ms))
                        {
                            xe.WriteTo(xw);
                            xw.Flush();
                            ms.Flush();
                            this.m_decompressedContent = ms.ToArray();
                        }
                        break;
                    default:
                        this.m_decompressedContent = value;
                        break;
                }
            }
        }


        /// <summary>
        /// Represent the asset as a string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("/{1}/{2}", AppletCollection.APPLET_SCHEME, this.Manifest?.Info?.Id, this.Name);
        }

    }

}

