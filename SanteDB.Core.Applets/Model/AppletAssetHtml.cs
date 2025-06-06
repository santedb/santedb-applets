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
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Model
{


    /// <summary>
    /// Applet asset XML 
    /// </summary>
    [XmlType(nameof(AppletAssetHtml), Namespace = "http://santedb.org/applet")]
    [XmlRoot(nameof(AppletAssetHtml), Namespace = "http://santedb.org/applet")]
    [ExcludeFromCodeCoverage]
    public class AppletAssetHtml
    {

        // Backing element for HTML
        private XElement m_html;

        /// <summary>
        /// Applet asset html
        /// </summary>
        public AppletAssetHtml()
        {
            this.Bundle = new List<string>();
            this.Script = new List<AssetScriptReference>();
            this.Style = new List<string>();
        }

        /// <summary>
        /// Gets or sets the title of the applet asset
        /// </summary>
        [XmlElement("title"), JsonProperty("title")]
        public List<LocaleString> Titles { get; set; }


        /// <summary>
        /// Gets the specified name
        /// </summary>
        public String GetTitle(String language, bool returnNuetralIfNotFound = true)
        {
            var str = this.Titles?.Find(o => o.Language == language);
            if (str == null && returnNuetralIfNotFound)
            {
                str = this.Titles?.Find(o => o.Language == null);
            }

            return str?.Value;
        }

        /// <summary>
        /// Gets or sets the references for the assets
        /// </summary>
        [XmlElement("bundle"), JsonIgnore]
        public List<String> Bundle { get; set; }

        /// <summary>
        /// Gets or sets the script
        /// </summary>
        [XmlElement("script"), JsonIgnore]
        public List<AssetScriptReference> Script { get; set; }

        /// <summary>
        /// Gets or sets the script
        /// </summary>
        [XmlElement("style"), JsonIgnore]
        public List<String> Style { get; set; }


        /// <summary>
        /// Gets one or more routes
        /// </summary>
        [XmlElement("view"), JsonIgnore]
        public AppletViewState ViewState { get; set; }

        /// <summary>
        /// Content of the element
        /// </summary>
        //[XmlAnyElement("body", Namespace = "http://www.w3.org/1999/xhtml")]
        //[XmlAnyElement("html", Namespace = "http://www.w3.org/1999/xhtml")]
        //[XmlAnyElement("div", Namespace = "http://www.w3.org/1999/xhtml")]
        [XmlElement("content"), JsonIgnore]
        public XElement Html
        {
            get
            {
                return this.m_html;
            }
            set
            {
                // HACK: In mono XElement is serialized differently than .NET let's detect that
                if (value.Name.LocalName == "content" && value.Name.Namespace == "http://santedb.org/applet")
                {
                    this.m_html = value.Elements().FirstOrDefault(o => o.Name.Namespace == "http://www.w3.org/1999/xhtml");
                }
                else
                {
                    this.m_html = value;
                }
            }
        }

        /// <summary>
        /// Identifies whether the asset is static
        /// </summary>
        [XmlAttribute("static"), JsonIgnore]
        public bool Static { get; set; }

    }
}