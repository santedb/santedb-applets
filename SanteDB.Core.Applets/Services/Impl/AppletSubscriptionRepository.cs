/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2021-8-27
 */

using SanteDB.Core.Applets.Model;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Subscription;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.Core.Applets.Services.Impl
{
    /// <summary>
    /// An implementation of the <see cref="ISubscriptionRepository"/> that loads definitions from applets
    /// </summary>
    public class AppletSubscriptionRepository : ISubscriptionRepository,  IDaemonService
    {
        // Subscription definitions
        private List<SubscriptionDefinition> m_subscriptionDefinitions;

        // Lock object
        private object m_lockObject = new object();

        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AppletSubscriptionRepository));

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Applet Based Server Subscription Manager";

        /// <summary>
        /// Returns true if this service is running
        /// </summary>
        public bool IsRunning => this.m_subscriptionDefinitions != null;

        /// <summary>
        /// Fired when the service is starting
        /// </summary>
        public event EventHandler Starting;

        /// <summary>
        /// Fired when the service has started
        /// </summary>
        public event EventHandler Started;

        /// <summary>
        /// Fired when the service is about to stop
        /// </summary>
        public event EventHandler Stopping;

        /// <summary>
        /// Fired when the service has stopped
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Find the specified object
        /// </summary>
        public IQueryResultSet<SubscriptionDefinition> Find(Expression<Func<SubscriptionDefinition, bool>> query)
        {
            return new MemoryQueryResultSet<SubscriptionDefinition>(this.m_subscriptionDefinitions?.Where(query.Compile()));
        }

        /// <summary>
        /// Find the specified subscription definitions
        /// </summary>
        [Obsolete("Use Find(Expression<Func<SubscriptionDefinition, bool>>)")]
        public IEnumerable<SubscriptionDefinition> Find(Expression<Func<SubscriptionDefinition, bool>> query, int offset, int? count, out int totalResults, params ModelSort<SubscriptionDefinition>[] orderBy)
        {
            var results = this.m_subscriptionDefinitions?.Where(query.Compile());
            totalResults = results.Count();
            return results.Skip(offset).Take(count ?? 100);
        }

        /// <summary>
        /// Get the specified definition
        /// </summary>
        public SubscriptionDefinition Get(Guid key)
        {
            return this.m_subscriptionDefinitions.FirstOrDefault(o => o.Key == key);
        }

        /// <summary>
        /// Gets the specified definition
        /// </summary>
        public SubscriptionDefinition Get(Guid key, Guid versionKey)
        {
            return this.Get(key);
        }

        /// <summary>
        /// Insert the specified definition
        /// </summary>
        public SubscriptionDefinition Insert(SubscriptionDefinition data)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Obsolete the specified definition
        /// </summary>
        public SubscriptionDefinition Obsolete(Guid key)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Obsolete the specified definition
        /// </summary>
        public SubscriptionDefinition Delete(Guid key)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Save the specified definition
        /// </summary>
        public SubscriptionDefinition Save(SubscriptionDefinition data)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Start the service
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            this.m_subscriptionDefinitions = new List<SubscriptionDefinition>();
            // Subscribe to the applet manager
            EventHandler loaderFn = (o, e) =>
            {
                var retVal = new List<SubscriptionDefinition>(this.m_subscriptionDefinitions?.Count ?? 10);
                var slns = ApplicationServiceContext.Current.GetService<IAppletSolutionManagerService>().Solutions.ToList();
                slns.Add(new AppletSolution() { Meta = new AppletInfo() { Id = String.Empty } });

                foreach (var s in slns)
                {
                    var slnMgr = ApplicationServiceContext.Current.GetService<IAppletSolutionManagerService>().GetApplets(s.Meta.Id);
                    // Find and load all sub defn's
                    foreach (var am in slnMgr)
                    {
                        var definitions = am.Assets.Where(a => a.Name.StartsWith("subscription/")).Select<AppletAsset, SubscriptionDefinition>(a =>
                        {
                            using (var ms = new MemoryStream(slnMgr.RenderAssetContent(a)))
                            {
                                this.m_tracer.TraceInfo("Attempting load of {0}", a.Name);
                                try
                                {
                                    return SubscriptionDefinition.Load(ms);
                                }
                                catch (Exception ex)
                                {
                                    this.m_tracer.TraceError("Error loading {0} : {1}", a.Name, ex);
                                    return null;
                                }
                            }
                        }).OfType<SubscriptionDefinition>();

                        retVal.AddRange(definitions.Where(n => !retVal.Any(a => a.Key == n.Key)));
                    }
                }

                this.m_tracer.TraceInfo("Registering applet subscriptions");

                lock (m_lockObject)
                {
                    this.m_subscriptionDefinitions.Clear();
                    this.m_subscriptionDefinitions.AddRange(retVal);
                }
            };

            ApplicationServiceContext.Current.Started += (o, e) =>
            {
                // Collection changed handler
                this.m_tracer.TraceInfo("Binding to change events");
                var appletService = ApplicationServiceContext.Current.GetService<IAppletManagerService>();

                if (appletService == null)
                    throw new InvalidOperationException("No applet manager service has been loaded!!!!");

                ApplicationServiceContext.Current.GetService<IAppletManagerService>().Changed += loaderFn;

                if ((ApplicationServiceContext.Current.GetService<IAppletManagerService>() as IDaemonService)?.IsRunning == false)
                    (ApplicationServiceContext.Current.GetService<IAppletManagerService>() as IDaemonService).Started += loaderFn;
                else
                    loaderFn(this, EventArgs.Empty);
            };
            return true;
        }

        /// <summary>
        /// Stop the service
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);
            this.m_subscriptionDefinitions = null;
            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}