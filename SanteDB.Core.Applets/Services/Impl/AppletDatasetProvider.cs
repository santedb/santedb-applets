using SanteDB.Core.Data.Initialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SanteDB.Core.Applets.Services.Impl
{
    /// <summary>
    /// An implementation of the <see cref="IDatasetProvider"/> which loads dataset files from applets
    /// </summary>
    public class AppletDatasetProvider : IDatasetProvider
    {
        private readonly IAppletManagerService m_appletManager;
        private readonly IAppletSolutionManagerService m_solutionManager;

        /// <summary>
        /// DI constructor
        /// </summary>
        public AppletDatasetProvider(IAppletManagerService appletManager, IAppletSolutionManagerService solutionManager)
        {
            this.m_appletManager = appletManager;
            this.m_solutionManager = solutionManager;
        }

        /// <summary>
        /// Get all datasets
        /// </summary>
        public IEnumerable<Dataset> GetDatasets()
        {
            return this.m_solutionManager.Solutions
                .SelectMany(o => this.m_solutionManager.GetApplets(o.Meta.Id))
                .Union(this.m_appletManager.Applets)
                .SelectMany(o => o.Assets.Where(a => a.Name.EndsWith(".dataset")))
                .Select(o =>
                {
                    using (var ms = new MemoryStream(this.m_appletManager.Applets.RenderAssetContent(o)))
                    {
                        return Dataset.Load(ms);
                    }
                });
        }
    }
}
