using SanteDB.Core.Applets.Configuration;
using SanteDB.Core.Data.Import.Definition;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace SanteDB.Core.Applets.Services.Impl
{
    /// <summary>
    /// Applet foreign data map repository 
    /// </summary>
    public class AppletForeignDataMapRepository : IRepositoryService<ForeignDataMap>
    {
        private readonly IAppletManagerService m_appletManagerService;
        private readonly IAppletSolutionManagerService m_appletSolutionManagerService;
        private List<ForeignDataMap> m_definitionCache = new List<ForeignDataMap>();

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(AppletForeignDataMapRepository));

        /// <summary>
        /// Applet foreign data map repository
        /// </summary>
        public AppletForeignDataMapRepository(IAppletManagerService appletManagerService, IAppletSolutionManagerService appletSolutionManagerService = null)
        {
            this.m_appletManagerService = appletManagerService;
            this.m_appletSolutionManagerService = appletSolutionManagerService;

            // Re-scans the loaded applets for definitions when the collection has changed
            this.m_appletManagerService.Changed += (oa, ea) =>
            {
                this.LoadAllDefinitions();
            };

            if (this.m_appletSolutionManagerService != null && this.m_appletSolutionManagerService.Solutions is INotifyCollectionChanged notify)
            {
                notify.CollectionChanged += (oa, eo) =>
                {
                    this.LoadAllDefinitions();
                };
            }
            this.LoadAllDefinitions();
        }

        /// <summary>
        /// Load all definitions
        /// </summary>
        private void LoadAllDefinitions()
        {
            this.m_definitionCache.Clear();
            using (AuthenticationContext.EnterSystemContext())
            {
                this.m_tracer.TraceInfo("Re-loading foreign data maps");
                // We only want to clear those assets which can be defined in applets
                var solutions = this.m_appletSolutionManagerService?.Solutions.ToList();

                // Doesn't have a solution manager
                if (!solutions.Any())
                {
                    this.ProcessApplet(this.m_appletManagerService.Applets);
                }
                else
                {
                    solutions.Add(new Core.Applets.Model.AppletSolution() { Meta = new Core.Applets.Model.AppletInfo() { Id = String.Empty } });
                    foreach (var s in solutions)
                    {
                        var appletCollection = this.m_appletSolutionManagerService.GetApplets(s.Meta.Id);
                        this.ProcessApplet(appletCollection);
                    }
                }
            }
        }

        /// <summary>
        /// Process applets - 
        /// </summary>
        private void ProcessApplet(ReadonlyAppletCollection applets)
        {
            this.m_definitionCache = this.m_definitionCache.Union(applets.SelectMany(o => o.Assets)
                .Where(o => o.Name.StartsWith("alien/") && o.Name.EndsWith(".xml"))
                .Select(o =>
                {
                    try
                    {
                        this.m_tracer.TraceVerbose("Attempting to load {0}", o.Name);
                        using (var ms = new MemoryStream(applets.RenderAssetContent(o)))
                        {
                            var fdm = ForeignDataMap.Load(ms);
                            if(!fdm.Key.HasValue)
                            {
                                this.m_tracer.TraceWarning("Could not load {0} - missing UUID", fdm.Name ?? o.Name);
                                return null;
                            }
                            else
                            {
                                return fdm;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        this.m_tracer.TraceWarning("Could not load FDM Definition: {0} : {1}", o.Name, e);
                        return null;
                    }
                }))
                .GroupBy(o => o.Key)
                .Select(o=>o.First())
                .ToList();

        }

        /// <inheritdoc/>
        public string ServiceName => "Applet Based Foreign Data Map";

        /// <inheritdoc/>
        public ForeignDataMap Delete(Guid key)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IQueryResultSet<ForeignDataMap> Find(Expression<Func<ForeignDataMap, bool>> query) => this.m_definitionCache.Where(query.Compile()).AsResultSet();

        /// <inheritdoc/>
        public ForeignDataMap Get(Guid key) => this.Get(key, Guid.Empty);

        /// <inheritdoc/>
        public ForeignDataMap Get(Guid key, Guid versionKey)
        {
            return this.m_definitionCache.Find(o => o.Key == key);
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This template repository cannot be added to</exception>
        public ForeignDataMap Insert(ForeignDataMap data)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This template repository cannot be added to</exception>
        public ForeignDataMap Save(ForeignDataMap data)
        {
            throw new NotSupportedException();
        }
    }
}
