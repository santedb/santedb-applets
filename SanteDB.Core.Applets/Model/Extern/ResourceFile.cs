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
