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
using Newtonsoft.Json;
using SanteDB.Core.Model.Acts;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Model
{
    /// <summary>
    /// Applet care pathway definition 
    /// </summary>
    [XmlType(nameof(AppletCarePathwayDefinition), Namespace = "http://santedb.org/applet")]
    [ExcludeFromCodeCoverage]
    public class AppletCarePathwayDefinition
    {

        /// <summary>
        /// Gets or sets the UUID of the definition
        /// </summary>
        [XmlElement("uuid"), JsonProperty("uuid")]
        public Guid Uuid { get; set; }

        /// <summary>
        /// Gets or sets the unique the mnemonic
        /// </summary>
        [XmlAttribute("mnemonic"), JsonProperty("mnemonic")]
        public String Mnemonic { get; set; }

        /// <summary>
        /// Gets or sets teh description
        /// </summary>
        [XmlElement("description"), JsonProperty("description")]
        public String Description { get; set; }

        /// <summary>
        /// Gets the elegilibty criteria
        /// </summary>
        [XmlElement("eligibility"), JsonProperty("eligibility")]
        public String EligibilityCriteria { get; set; }

        /// <summary>
        /// Gets or sets the enrolment mode
        /// </summary>
        [XmlElement("enrollment"), JsonProperty("enrollment")]
        public CarePathwayEnrollmentMode EnrollmentMode { get; set; }

        /// <summary>
        /// Gets or sets the encounter template
        /// </summary>
        [XmlElement("encounter"), JsonProperty("encounter")]
        public String EncounterTemplate { get; set; }

    }
}
