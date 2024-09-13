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
        [XmlElement("enrolment"), JsonProperty("enrolment")]
        public CarePathwayEnrolmentMode EnrolmentMode { get; set; }

    }
}
