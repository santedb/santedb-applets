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
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Services.Impl
{
    /// <summary>
    /// An implementation of the ISubscriptionRepository that loads definitions from applets
    /// </summary>
    public class AppletSubscriptionRepository : IRepositoryService<SubscriptionDefinition>, IDaemonService
    {

        // Subscription definitions
        private List<SubscriptionDefinition> m_subscriptionDefinitions;

        // Lock object
        private object m_lockObject = new object();

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(AppletSubscriptionRepository));

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
        public IEnumerable<SubscriptionDefinition> Find(Expression<Func<SubscriptionDefinition, bool>> query)
        {
            int tr;
            return this.Find(query, 0, 100, out tr);
        }

        /// <summary>
        /// Find the specified subscription definitions
        /// </summary>
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
            var appletMgr = ApplicationServiceContext.Current.GetService<IAppletManagerService>().Applets;
            // Subscribe to the applet manager
            System.Collections.Specialized.NotifyCollectionChangedEventHandler loaderFn = (o, e) =>
            {
                // Find and load all sub defn's
                foreach (AppletManifest am in (e.NewItems ?? new AppletManifest[0]).OfType<AppletManifest>().Union(e.OldItems?.OfType<AppletManifest>() ?? new AppletManifest[0]))
                {
                    var definitions = am.Assets.Where(a => a.Name.StartsWith("subscription/")).Select<AppletAsset, SubscriptionDefinition>(a =>
                    {
                        using (var ms = new MemoryStream(appletMgr.RenderAssetContent(a)))
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

                    // Perform tasks
                    lock (this.m_lockObject)
                    {
                        switch (e.Action)
                        {
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                                this.m_subscriptionDefinitions.RemoveAll(s => definitions.Any(d => d.Key == s.Key));
                                this.m_subscriptionDefinitions.AddRange(definitions);
                                break;
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                                // Unload 
                                this.m_subscriptionDefinitions.RemoveAll(s => definitions.Any(d => d.Key == s.Key));
                                break;

                        }
                    }
                }
                
            };

            // Get the current applets
            loaderFn(this, new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Add, appletMgr.OfType<AppletManifest>().ToList()));

            // Collection changed handler
            appletMgr.CollectionChanged += loaderFn;

            this.Started?.Invoke(this, EventArgs.Empty);
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
