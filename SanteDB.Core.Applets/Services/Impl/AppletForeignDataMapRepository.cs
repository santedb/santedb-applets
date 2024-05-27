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
 * User: fyfej
 * Date: 2023-6-21
 */
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
            try
            {
                this.m_definitionCache = this.m_definitionCache.Union(applets.SelectMany(o => o.Assets)
                    .Where(o => (o.Name.StartsWith("alien/") || o.Name.StartsWith("fdm/")) && o.Name.EndsWith(".xml"))
                    .Select(o =>
                    {
                        try
                        {
                            this.m_tracer.TraceVerbose("Attempting to load {0}", o.Name);
                            using (var ms = new MemoryStream(applets.RenderAssetContent(o)))
                            {
                                var fdm = ForeignDataMap.Load(ms);
                                if (!fdm.Key.HasValue)
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
                    .OfType<ForeignDataMap>()
                    .GroupBy(o => o.Key)
                    .Select(o => o.First())
                    .ToList();
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error processing applet definitions - {0}", e);
            }

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
            using (var ms = new MemoryStream())
            {
                var map = this.m_definitionCache.Find(o => o.Key == key);
                if (map == null)
                {
                    throw new KeyNotFoundException(key.ToString());
                }
                map.Save(ms);
                ms.Seek(0, SeekOrigin.Begin);
                return ForeignDataMap.Load(ms);
            }
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
