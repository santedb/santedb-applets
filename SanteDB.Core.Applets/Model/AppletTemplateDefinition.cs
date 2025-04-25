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
using SanteDB.Core.Model.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Model
{

    /// <summary>
    /// Applet template definition
    /// </summary>
    [XmlType(nameof(AppletTemplateDefinition), Namespace = "http://santedb.org/applet")]
    [ExcludeFromCodeCoverage]
    public class AppletTemplateDefinition
    {

        /// <summary>
        /// Public
        /// </summary>
        [XmlAttribute("public"), JsonProperty("public"), QueryParameter("public")]
        public bool Public { get; set; }

        /// <summary>
        /// Gets or sets the mnemonic
        /// </summary>
        [XmlAttribute("mnemonic"), JsonProperty("mnemonic"), QueryParameter("mnemonic")]
        public String Mnemonic { get; set; }

        /// <summary>
        /// Gets or sets the form
        /// </summary>
        [XmlElement("form"), JsonProperty("form")]
        public String Form { get; set; }

        /// <summary>
        /// Gets or sets the form
        /// </summary>
        [XmlElement("view"), JsonProperty("view")]
        public String View { get; set; }

        /// <summary>
        /// Summary view
        /// </summary>
        [XmlElement("summaryView"), JsonProperty("summaryView")]
        public String Summary { get; set; }

        /// <summary>
        /// Back-entry forms
        /// </summary>
        [XmlElement("backEntry"), JsonProperty("backEntry")]
        public String BackEntry { get; set; }

        /// <summary>
        /// Gets or sets the definition file
        /// </summary>
        [XmlElement("definition"), JsonProperty("definition")]
        public String Definition { get; set; }

        /// <summary>
        /// Gets or sets the description of the template
        /// </summary>
        [XmlElement("description"), JsonProperty("description")]
        public String Description { get; set; }

        /// <summary>
        /// Gets or sets the definition
        /// </summary>
        [XmlElement("oid"), JsonProperty("oid")]
        public String Oid { get; set; }

        /// <summary>
        /// Gets the UUID for the template
        /// </summary>
        [XmlElement("uuid"), JsonProperty("uuid")]
        public Guid Uuid { get; set; }


        /// <summary>
        /// Gets the guard condition for the template executed against the patient (allows for blocking of
        /// templates based on patient conditions)
        /// </summary>
        [XmlElement("guard"), JsonProperty("guard")]
        public String Guard { get; set; }

        /// <summary>
        /// Gets or sets the scope of the encounter in which the data is being collected
        /// </summary>
        [XmlElement("scope"), JsonProperty("scope")]
        public List<String> Scope { get; set; }

        /// <summary>
        /// Gets or sets the priority of the template (for overrides)
        /// </summary>
        [XmlAttribute("priority"), JsonProperty("priority")]
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the priority of the template (for overrides)
        /// </summary>
        [XmlElement("icon"), JsonProperty("icon")]
        public string Icon { get; set; }

        /// <summary>
        /// Applet manifest
        /// </summary>
        [XmlIgnore]
        public AppletManifest Manifest { get; internal set; }

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="host"></param>
        internal void Initialize(AppletManifest host)
        {
            this.Manifest = host;
        }
    }
}
