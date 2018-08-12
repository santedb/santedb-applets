using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Model
{
    /// <summary>
    /// Represents a solution (or collection of applets)
    /// </summary>
    [XmlType(nameof(AppletSolution), Namespace = "http://santedb.org/applet")]
    [XmlRoot(nameof(AppletSolution), Namespace = "http://santedb.org/applet")]
    public class AppletSolution : AppletPackage
    {

        /// <summary>
        /// Gets or sets the list of applets that are to be included in this package
        /// </summary>
        [XmlElement("include")]
        public List<AppletPackage> Include { get; set; }

    }
}
