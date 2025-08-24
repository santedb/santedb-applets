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
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Applets.Services.Impl;
using SanteDB.Core.Configuration;
using SanteDB.Core.Configuration.Features;
using SanteDB.Core.Data.Import.Definition;
using SanteDB.Core.Model.Subscription;
using SanteDB.Core.Notifications;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SanteDB.Core.Applets.Configuration
{
    /// <summary>
    /// Represents the local applet manager feature
    /// </summary>
    public class LocalAppletManagerFeature : GenericServiceFeature<FileSystemAppletManagerService>
    {

        /// <summary>
        /// Create a new local applet manager 
        /// </summary>
        public LocalAppletManagerFeature()
        {
        }

        /// <summary>
        /// Create installation tasks
        /// </summary>
        public override IEnumerable<IConfigurationTask> CreateInstallTasks()
        {
            var conf = this.Configuration as AppletConfigurationSection ?? new AppletConfigurationSection()
            {
                AppletDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "applets")
            };

            yield return new InstallTask<AppletDataReferenceResolver>(this, (c) => !c.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Any(t => typeof(IReferenceResolver).IsAssignableFrom(t.Type)));
            yield return new InstallTask<FileSystemAppletManagerService>(this, (c) => !c.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Any(t => typeof(IAppletManagerService).IsAssignableFrom(t.Type)));
            yield return new InstallTask<AppletSubscriptionRepository>(this, (c) => !c.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Any(t => typeof(IRepositoryService<SubscriptionDefinition>).IsAssignableFrom(t.Type)));
            yield return new InstallTask<AppletNotificationTemplateRepository>(this, (c) => !c.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Any(t => typeof(INotificationTemplateRepository).IsAssignableFrom(t.Type)));
            yield return new InstallTask<AppletForeignDataMapRepository>(this, (c) => !c.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders.Any(t => typeof(IRepositoryService<ForeignDataMap>).IsAssignableFrom(t.Type)));
        }

        /// <summary>
        /// Create uninstall tasks
        /// </summary>
        public override IEnumerable<IConfigurationTask> CreateUninstallTasks()
        {
            yield return new UninstallTask<FileSystemAppletManagerService>(this);
            yield return new UninstallTask<AppletForeignDataMapRepository>(this);

        }
        /// <summary>
        /// Auto-setup the applet features
        /// </summary>
        public override FeatureFlags Flags => FeatureFlags.AutoSetup | FeatureFlags.AlwaysConfigure;

        /// <summary>
        /// Get the configuration type
        /// </summary>
        public override Type ConfigurationType => typeof(AppletConfigurationSection);

        /// <summary>
        /// Get the group this feature belongs to
        /// </summary>
        public override string Group => FeatureGroup.System;

        /// <summary>
        /// Get default configuration
        /// </summary>
        protected override object GetDefaultConfiguration() => new AppletConfigurationSection();
    }
}
