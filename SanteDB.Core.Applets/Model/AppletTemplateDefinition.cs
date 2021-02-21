/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */
using Newtonsoft.Json;
using SanteDB.Core.Model.Attributes;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Model
{

    /// <summary>
    /// Applet template definition
    /// </summary>
    [XmlType(nameof(AppletTemplateDefinition), Namespace = "http://santedb.org/applet")]
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
        [XmlElement("form"),JsonProperty("form")]
        public String Form { get; set; }

        /// <summary>
        /// Gets or sets the form
        /// </summary>
        [XmlElement("view"), JsonProperty("view")]
        public String View { get; set; }

        /// <summary>
        /// Gets or sets the definition
        /// </summary>
        [XmlElement("definition"), JsonProperty("definition")]
        public String Definition { get; set; }

        /// <summary>
        /// Gets or sets the definition
        /// </summary>
        [XmlElement("description"), JsonProperty("description")]
        public String Description { get; set; }

        /// <summary>
        /// Gets or sets the definition
        /// </summary>
        [XmlElement("oid"), JsonProperty("oid")]
        public String Oid { get; set; }

        /// <summary>
        /// Identifies allowed content (templates) which can be embedded in this template
        /// </summary>
        [XmlElement("allowComponent"), JsonProperty("allowComponent")]
        public List<String> AllowComponent { get; set; }

        /// <summary>
        /// Gets the guard condition for the template executed against the patient (allows for blocking of
        /// templates based on patient conditions)
        /// </summary>
        [XmlElement("guard"), JsonProperty("guard")]
        public String Guard { get; set; }

        /// <summary>
        /// Gets the guard condition for the template executed against the patient (allows for blocking of
        /// templates based on patient conditions)
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

    }
}
