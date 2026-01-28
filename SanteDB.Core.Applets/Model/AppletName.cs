/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Model
{
    /// <summary>
    /// Applet reference
    /// </summary>
    [XmlType(nameof(AppletName), Namespace = "http://santedb.org/applet"), JsonObject]
    [ExcludeFromCodeCoverage]
    public class AppletName
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.Core.Applets.Model.AppletName"/> class.
        /// </summary>
        public AppletName()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.Core.Applets.Model.AppletName"/> class.
        /// </summary>
        public AppletName(String id, String version, String publicKeyToken)
        {
            this.Id = id;
            this.Version = version;
            this.PublicKeyToken = publicKeyToken;
        }

        /// <summary>
        /// The identifier of the applet
        /// </summary>
        [XmlAttribute("id")]
        [JsonProperty("id")]
        public String Id
        {
            get;
            set;
        }

        /// <summary>
        /// The version of the applet
        /// </summary>
        [XmlAttribute("version")]
        [JsonProperty("version")]
        public String Version
        {
            get;
            set;
        }

        /// <summary>
        /// The signature of the applet (not used for verification, rather lookup)
        /// </summary>
        /// <value>The signature.</value>
        [XmlAttribute("publicKeyToken")]
        [JsonProperty("publicKeyToken")]
        public String PublicKeyToken
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the signature which can be used to validate the file
        /// </summary>
        [XmlElement("signature")]
        [JsonIgnore]
        public byte[] Signature
        {
            get;
            set;
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return string.Format("Id={0}, Version={1}, PublicKeyToken={2}", Id, Version, PublicKeyToken);
        }

        /// <summary>
        /// Parse a reference string 
        /// </summary>
        /// <param name="appletNameString">The name of the string in format id:version</param>
        /// <returns>The constructed applet name</returns>
        public static AppletName Parse(string appletNameString)
        {
            var nameParts = appletNameString.Split(':');
            if (nameParts.Length == 1)
            {
                return new AppletName(nameParts[0], null, null);
            }
            else
            {
                return new AppletName(nameParts[0], nameParts[1] == "*" ? null : nameParts[1], null);
            }
        }

        /// <summary>
        /// Get the <see cref="Version"/> property as a <see cref="System.Version"/> instance
        /// </summary>
        /// <returns>The <see cref="System.Version"/> of the parsed <see cref="Version"/> property</returns>
        public Version GetVersion()
        {
            System.Version.TryParse(this.Version, out var result);
            return result;
        }
    }
}