/*
 * Copyright (C) 2021 - 2021, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-5
 */
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Model
{
    /// <summary>
    /// Applet strings
    /// </summary>
    [XmlType(nameof(AppletStrings), Namespace = "http://santedb.org/applet")]
    [JsonObject]
    [ExcludeFromCodeCoverage]
    public class AppletStrings
    {

        /// <summary>
        /// Language of the strings
        /// </summary>
        [XmlAttribute("lang"), JsonProperty("lang")]
        public string Language { get; set; }

        /// <summary>
        /// Reference for the translation strings file
        /// </summary>
        [XmlAttribute("extern")]
        public string Reference { get; set; }

        /// <summary>
        /// Gets or sets the string
        /// </summary>
        [XmlElement("string"), JsonProperty("string")]
        public List<AppletStringData> String { get; set; }

    }

    /// <summary>
    /// Applet string data
    /// </summary>
    [XmlType(nameof(AppletStringData), Namespace = "http://santedb.org/applet")]
    [JsonObject]
    [ExcludeFromCodeCoverage]
    public class AppletStringData
    {
        /// <summary>
        /// Gets or sets the key
        /// </summary>
        [XmlAttribute("key"), JsonProperty("key")]
        public string Key { get; set; }

        /// <summary>
        /// Priority of the string
        /// </summary>
        [XmlAttribute("priority"), JsonProperty("priority")]
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the value of the property
        /// </summary>
        [XmlText, JsonProperty("value")]
        public string Value { get; set; }

    }
}