/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-3-10
 */
using System;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Model
{

    /// <summary>
    /// Applet configuration entry
    /// </summary>
    [XmlType(nameof(AppletSettingEntry), Namespace = "http://santedb.org/applet")]
    [ExcludeFromCodeCoverage]
    public class AppletSettingEntry
    {

        /// <summary>
        /// Default constructor
        /// </summary>
        public AppletSettingEntry()
        {
        }

        /// <summary>
        /// Create a new setting entry
        /// </summary>
        /// <param name="name">The name of the setting</param>
        /// <param name="value">The value of the setting</param>
        public AppletSettingEntry(string name, string value)
        {
            this.Name = name;
            this.Value = value;
        }

        /// <summary>
        /// The name of the property
        /// </summary>
        /// <value>The name.</value>
        [XmlAttribute("name")]
        public String Name
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        [XmlAttribute("value")]
        public String Value
        {
            get;
            set;
        }
    }

}
