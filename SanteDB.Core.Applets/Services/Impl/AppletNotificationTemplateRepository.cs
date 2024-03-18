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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Notifications;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.Core.Applets.Services.Impl
{
    /// <summary>
    /// An implementation of the <see cref="INotificationTemplateRepository"/> which loads <see cref="NotificationTemplate"/> instances
    /// from the <c>notification/</c> folder in applets
    /// </summary>
    public class AppletNotificationTemplateRepository : INotificationTemplateRepository, IDaemonService
    {
        private readonly IAppletManagerService m_appletManagerService;
        private readonly IAppletSolutionManagerService m_appletSolutionManagerService;
        private readonly ConcurrentDictionary<String, NotificationTemplate> m_definitionCache = new ConcurrentDictionary<String, NotificationTemplate>();
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AppletNotificationTemplateRepository));

        /// <inheritdoc/>
        public event EventHandler Starting;
        /// <inheritdoc/>
        public event EventHandler Started;
        /// <inheritdoc/>
        public event EventHandler Stopping;
        /// <inheritdoc/>
        public event EventHandler Stopped;

        /// <summary>
        /// DI constructor
        /// </summary>
        public AppletNotificationTemplateRepository(IAppletManagerService appletManager, IAppletSolutionManagerService appletSolutionManagerService)
        {
            this.m_appletManagerService = appletManager;
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
        /// Loads all template definitions from the applet collection
        /// </summary>
        private void LoadAllDefinitions()
        {
            using (AuthenticationContext.EnterSystemContext())
            {
                this.m_tracer.TraceInfo("Re-loading notification template");
                // We only want to clear those assets which can be defined in applets
                var solutions = this.m_appletSolutionManagerService?.Solutions.ToList();

                // Doesn't have a solution manager
                if (solutions == null)
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
        /// Process contents of the <paramref name="appletAssets"/> and register the notification templates
        /// </summary>
        private void ProcessApplet(ReadonlyAppletCollection appletAssets)
        {
            if (appletAssets == null)
            {
                throw new ArgumentNullException(nameof(appletAssets));
            }

            foreach (var asset in appletAssets.SelectMany(o => o.Assets.Where(a => a.Name.StartsWith("notifications/"))))
            {
                try
                {
                    using (var str = new MemoryStream(appletAssets.RenderAssetContent(asset)))
                    {
                        var notification = NotificationTemplate.Load(str);
                        this.m_definitionCache.TryAdd(notification.Id, notification); // set the default with no language
                        if (!this.m_definitionCache.TryAdd($"{notification.Id}/{notification.Language}", notification))
                        {
                            this.m_tracer.TraceWarning("Could not add {0} since it already is registered by another applet", notification.Id);
                        }

                    }
                }
                catch (Exception)
                {
                    this.m_tracer.TraceError("Could not load notification template {0}", asset.Name);
                }
            }
        }

        /// <inheritdoc/>
        public string ServiceName => "Applet Notification Repository";

        /// <inheritdoc/>
        public bool IsRunning => true;

        /// <inheritdoc/>
        public IEnumerable<NotificationTemplate> Find(Expression<Func<NotificationTemplate, bool>> filter) => this.m_definitionCache.Values.Where(filter.Compile());

        /// <inheritdoc/>
        public NotificationTemplate Get(string id, string lang)
        {
            if (this.m_definitionCache.TryGetValue($"{id}/{lang}", out var retVal) ||
                this.m_definitionCache.TryGetValue(id, out retVal))
            {
                return retVal;
            }
            return null;
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This template repository cannot be added to</exception>
        public NotificationTemplate Insert(NotificationTemplate template)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This template repository cannot be added to</exception>
        public NotificationTemplate Update(NotificationTemplate template)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public bool Start()
        {
            Starting?.Invoke(this, EventArgs.Empty);

            this.LoadAllDefinitions();

            Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <inheritdoc/>
        public bool Stop()
        {
            Stopping?.Invoke(this, EventArgs.Empty);

            Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}
