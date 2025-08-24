/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using SanteDB.Core;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Services;
using System.IO;
using System.Linq;
using System.Text;

namespace SanteDB.Core.Applets.Services.Impl
{
    /// <summary>
    /// Represents a <see cref="IDataReferenceResolver"/> which resolves data assets from the 
    /// installed applets in the SanteDB <see cref="IAppletManagerService"/>
    /// </summary>
    public class AppletDataReferenceResolver : IReferenceResolver
    {
        private readonly IAppletManagerService m_appletManager;

        /// <summary>
        /// DI CTOR
        /// </summary>
        public AppletDataReferenceResolver(IAppletManagerService appletManager = null)
        {
            this.m_appletManager = appletManager;
        }

        /// <inheritdoc/>
        public Stream ResolveAsStream(string reference) => Resolve(reference);

        /// <inheritdoc/>
        public string ResolveAsString(string reference)
        {
            using (var sr = new StreamReader(Resolve(reference)))
            {
                return sr.ReadToEnd();
            }
        }

        /// <summary>
        /// Resolve asset
        /// </summary>
        private Stream Resolve(string reference)
        {
            // From loaded applets we resolve the references
            var itm = this.m_appletManager?.Applets.ResolveAsset(reference) ?? this.m_appletManager.Applets.SelectMany(a => a.Assets).FirstOrDefault(a => a.Name.EndsWith(reference));
            if (itm == null)
            {
                return null;
            }

            return new MemoryStream(this.m_appletManager.Applets.RenderAssetContent(itm));
        }

    }
}
