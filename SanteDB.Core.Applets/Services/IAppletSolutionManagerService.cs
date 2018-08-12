using SanteDB.Core.Applets.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Core.Applets.Services
{
    /// <summary>
    /// Represents a service which loads/registers applet solutions
    /// </summary>
    public interface IAppletSolutionManagerService
    {
        /// <summary>
        /// Get the solutions configured on the server
        /// </summary>
        IEnumerable<AppletSolution> Solutions { get; }

        /// <summary>
        /// Uninstall a package
        /// </summary>
        bool UnInstall(String solutionId);

        /// <summary>
        /// Installs or upgrades an existing applet collection via package
        /// </summary>
        bool Install(AppletSolution solution, bool isUpgrade = false);

    }
}
