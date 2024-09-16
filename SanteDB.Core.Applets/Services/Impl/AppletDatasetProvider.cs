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
