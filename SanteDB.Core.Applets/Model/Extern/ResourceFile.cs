﻿/*
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
 * Date: 2021-8-22
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Core.Applets.Model.Extern
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Xml.Serialization;

    namespace SanteDB.Core.Applets.Model
    {
        /// <summary>
        /// Applet string resource file
        /// </summary>
        [XmlRoot("resources")]
        [XmlType(nameof(ResourceFile))]
        public class ResourceFile
        {

            // Serializer
            private static readonly XmlSerializer m_xsz = new XmlSerializer(typeof(ResourceFile));

            /// <summary>
            /// Gets or sets the strings
            /// </summary>
            [XmlElement("string")]
            public List<ExternalStringResource> Strings { get; set; }

            /// <summary>
            /// Load a resource file
            /// </summary>
            public static ResourceFile Load(Stream inputStream)
            {
                return m_xsz.Deserialize(inputStream) as ResourceFile;
            }
        }

        /// <summary>
        /// Represents the string data
        /// </summary>
        [XmlType(nameof(ExternalStringResource))]
        public class ExternalStringResource
        {

            /// <summary>
            /// Gets or sets the name of the key
            /// </summary>
            [XmlAttribute("name")]
            public string Key { get; set; }

            /// <summary>
            /// Gets or sets the value of the string
            /// </summary>
            [XmlText]
            public string Value { get; set; }
        }
    }

}