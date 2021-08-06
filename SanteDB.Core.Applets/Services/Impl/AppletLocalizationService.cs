using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Model.Extern.SanteDB.Core.Applets.Model;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SanteDB.Core.Applets.Services.Impl
{
    /// <summary>
    /// Applet localization
    /// </summary>
    public class AppletLocalizationService : ILocalizationService
    {

        // Applet localization service
        private Tracer m_tracer = Tracer.GetTracer(typeof(AppletLocalizationService));

        /// <summary>
        /// String cache
        /// </summary>
        private ConcurrentDictionary<String, IDictionary<String,String>> m_stringCache = new ConcurrentDictionary<string, IDictionary<String, String>>();

        // Applet manager
        private IAppletManagerService m_appletManager;

        // Solution manager
        private IAppletSolutionManagerService m_solutionManager;

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Applet-Based Localization Service";

        /// <summary>
        /// Dependency injection header for localization service
        /// </summary>
        public AppletLocalizationService(IAppletManagerService appletManager, IAppletSolutionManagerService solutionManagerService = null)
        {
            this.m_appletManager = appletManager;
            this.m_solutionManager = solutionManagerService;
        }

        /// <summary>
        /// Format the specified string
        /// </summary>
        public string FormatString(string stringKey, params object[] parameters) => this.FormatString(null, stringKey, parameters);

        /// <summary>
        /// Format the string
        /// </summary>
        public string FormatString(string locale, string stringKey, params object[] parameters) => String.Format(this.GetString(locale, stringKey), parameters);

        /// <summary>
        /// Get string 
        /// </summary>
        public string GetString(string stringKey) => this.GetString(null, stringKey);

        /// <summary>
        /// Get string
        /// </summary>
        public string GetString(string locale, string stringKey)
        {
            var refData = this.GetOrLoadStringData(locale ?? Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName);
            if(refData.TryGetValue(stringKey, out String retVal))
            {
                return retVal;
            }
            else
            {
                return stringKey;
            }
        }

        /// <summary>
        /// Get all strings for the specified locale
        /// </summary>
        public KeyValuePair<String,String>[] GetStrings(string locale)
        {
            return this.GetOrLoadStringData(locale).ToArray();
        }

        /// <summary>
        /// Get or load string data from the definition file
        /// </summary>
        private IDictionary<String, String> GetOrLoadStringData(string locale)
        {
            if (this.m_stringCache.TryGetValue(locale, out IDictionary<String, String> localStrings))
            {
                return localStrings;
            }
            else
            {

                if (this.m_solutionManager?.Solutions.Any() == true) // Load from solutions
                {
                    localStrings = this.m_solutionManager.Solutions
                        .SelectMany(s => this.m_solutionManager.GetApplets(s.Meta.Id))
                        .SelectMany(a => this.ResolveStringData(a, a.Strings.Where(l=>l.Language == locale)))
                        .GroupBy(s => s.Key)
                        .ToDictionary(o => o.Key, o => o.OrderByDescending(g => g.Priority).First().Value);
                }
                else
                {
                    localStrings = this.m_appletManager
                        .Applets
                        .SelectMany(a => this.ResolveStringData(a, a.Strings.Where(l => l.Language == locale)))
                        .GroupBy(s => s.Key)
                        .ToDictionary(o => o.Key, o => o.OrderByDescending(g => g.Priority).First().Value);
                }

                this.m_stringCache.TryAdd(locale, localStrings);
                return localStrings;
            }
        }

        /// <summary>
        /// Resolve string data
        /// </summary>
        private IEnumerable<AppletStringData> ResolveStringData(AppletManifest applet, IEnumerable<AppletStrings> stringResources)
        {
            foreach (var res in stringResources) {

                if (!String.IsNullOrEmpty(res.Reference))
                {
                    var asset = this.m_appletManager.Applets.ResolveAsset(res.Reference, applet);
                    if(asset == null)
                    {
                        this.m_tracer.TraceWarning($"Cannot resolve {res.Reference} - strings from this file will not be loaded");
                    }
                    else
                    {

                        ResourceFile resourceFile = null;
                        try
                        {
                            resourceFile = ResourceFile.Load(new MemoryStream(this.m_appletManager.Applets.RenderAssetContent(asset)));
                        }
                        catch (Exception e)
                        {
                            this.m_tracer.TraceWarning($"Could not process {asset} as a resources file - {e.Message} - Strings will not be loaded");
                        }

                        foreach(var externString in resourceFile?.Strings)
                        {
                            yield return new AppletStringData()
                            {
                                Key = externString.Key,
                                Value = externString.Value,
                                Priority = 1
                            };
                        }

                    }
                }
                else
                {
                    foreach(var internString in res.String)
                    {
                        yield return internString;
                    }
                }
            }
        }

        /// <summary>
        /// Reload the specified data
        /// </summary>
        public void Reload()
        {
            this.m_stringCache.Clear();
        }
    }
}
