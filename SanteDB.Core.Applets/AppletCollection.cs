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
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services.Impl;
using SanteDB.Core.Applets.ViewModel.Description;
using SanteDB.Core.Applets.ViewModel.Json;
using SanteDB.Core.Data.Quality;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Services;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace SanteDB.Core.Applets
{
    /// <summary>
    /// Represents a asset content resolver
    /// </summary>
    public delegate object AssetContentResolver(AppletAsset asset);

    /// <summary>
    /// A readonly applet collection
    /// </summary>
    public class ReadonlyAppletCollection : AppletCollection
    {
        /// <summary>
        /// Fired when the collection has changed
        /// </summary>
        public override event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Wrapper for the readonly applet collection
        /// </summary>
        internal ReadonlyAppletCollection(AppletCollection wrap)
        {
            this.m_appletManifest = wrap;
            wrap.CollectionChanged += (o, e) =>
            {
                if (o != this && e.Action == NotifyCollectionChangedAction.Reset)
                {
                    this.ClearCaches();
                }
            
                this.CollectionChanged?.Invoke(o, e);
            };
        }

        /// <summary>
        /// Clear all caches
        /// </summary>
        public override void ClearCaches()
        {
            base.ClearCaches();
        }

        /// <summary>
        /// Collection is readonly
        /// </summary>
        public override bool IsReadOnly
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Resolver
        /// </summary>
        public override AssetContentResolver Resolver
        {
            get
            {
                return (this.m_appletManifest as AppletCollection).Resolver;
            }
            set
            {
                throw new InvalidOperationException("Collection is readonly");
            }
        }

        /// <summary>
        /// Gets the base URL
        /// </summary>
        public override string BaseUrl
        {
            get
            {
                return (this.m_appletManifest as AppletCollection).BaseUrl;
            }
            set
            {
                throw new InvalidOperationException("Collection is readonly");
            }
        }

        /// <summary>
        /// Get the page caching setting
        /// </summary>
        public override bool CachePages
        {
            get
            {
                return (this.m_appletManifest as AppletCollection).CachePages;
            }
            set
            {
                throw new InvalidOperationException("Collection is readonly");
            }
        }

    }

    /// <summary>
    /// Represents a collection of applets
    /// </summary>
    public class AppletCollection : IList<AppletManifest>, INotifyCollectionChanged
    {
        // A cache of rendered assets
        private static ConcurrentDictionary<String, Byte[]> s_cache = new ConcurrentDictionary<string, byte[]>();

        private ConcurrentDictionary<String, AppletTemplateDefinition> s_templateCache = new ConcurrentDictionary<string, AppletTemplateDefinition>();
        private ConcurrentDictionary<String, ViewModelDescription> s_viewModelCache = new ConcurrentDictionary<string, ViewModelDescription>();
        private ConcurrentDictionary<String, IEnumerable<AppletAsset>> m_dynamicHtmlAssets = new ConcurrentDictionary<string, IEnumerable<AppletAsset>>();

        private List<AppletAsset> m_viewStateAssets = null;
        private List<AppletAsset> m_widgetAssets = null;
        private List<AppletAsset> m_htmlAssets = null;

        private AssetContentResolver m_resolver = null;
        private Regex m_localizationRegex = new Regex("{{\\s{0,}:?:?['\"]([A-Za-z0-9\\._\\-]*?)['\"]\\s{0,}\\|\\s?i18n\\s{0,}}}", RegexOptions.Compiled);
        private Regex m_bindingRegex = new Regex("{{\\s?\\$([A-Za-z0-9_]*?)\\s?}}", RegexOptions.Compiled);

        /// <summary>
        /// Represetns the applet scheme
        /// </summary>
        public const string APPLET_SCHEME = "app://";

        private string m_baseUrl = null;
        private bool m_cachePages = true;

        // XMLNS stuff
        private readonly XNamespace xs_xhtml = "http://www.w3.org/1999/xhtml";


        /// <summary>
        /// Gets or sets whether caching is enabled
        /// </summary>
        public virtual Boolean CachePages
        { get { return this.m_cachePages; } set { this.m_cachePages = value; } }

        /// <summary>
        /// Gets or sets the base url
        /// </summary>
        [ExcludeFromCodeCoverage]
        public virtual String BaseUrl
        {
            get { return this.m_baseUrl; }
            set
            {
                if (this.IsReadOnly)
                {
                    throw new InvalidOperationException("Collection is readonly");
                }

                this.m_baseUrl = value;
            }
        }

        /// <summary>
        /// Asset content resolver called when asset content is null
        /// </summary>
        [ExcludeFromCodeCoverage]
        public virtual AssetContentResolver Resolver
        {
            get { return this.m_resolver; }
            set
            {
                if (this.IsReadOnly)
                {
                    throw new InvalidOperationException("Collection is readonly");
                }

                this.m_resolver = value;
            }
        }

        // Reference bundles
        private List<RenderBundle> m_referenceBundles = new List<RenderBundle>()
        {
        };

        /// <summary>
        /// Constructs a new instance of the applet collection
        /// </summary>
        public AppletCollection()
        {
        }


        /// <summary>
        /// Applet collection rewrite to alternate url
        /// </summary>
        public AppletCollection(String baseUrl) : this()
        {
            this.m_baseUrl = baseUrl;
        }

        /// <summary>
        /// Clear all caches
        /// </summary>
        [ExcludeFromCodeCoverage]
        public virtual void ClearCaches()
        {
            m_viewStateAssets?.Clear();
            m_widgetAssets?.Clear();
            m_htmlAssets?.Clear();
            s_viewModelCache?.Clear();
            s_templateCache?.Clear();
            s_cache?.Clear();
            m_dynamicHtmlAssets.Clear();
            m_htmlAssets = null;
            m_viewStateAssets = null;
            m_widgetAssets = null;
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// The current default scope applet
        /// </summary>
        public AppletManifest DefaultApplet { get; set; }

        /// <summary>
        /// Represents the applet manifests in this collection
        /// </summary>
        protected IList<AppletManifest> m_appletManifest = new List<AppletManifest>();

        /// <summary>
        /// Fired when the collection has changed
        /// </summary>
        public virtual event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Gets or sets the item at the specified element
        /// </summary>
        [ExcludeFromCodeCoverage]
        public AppletManifest this[int index]
        {
            get { return this.m_appletManifest[index]; }
            set
            {
                this.m_appletManifest[index] = value;
                this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, index));
            }
        }

        /// <summary>
        /// Return the count of applets in the collection
        /// </summary>
        [ExcludeFromCodeCoverage]
        public int Count
        {
            get
            {
                return this.m_appletManifest.Count;
            }
        }

        /// <summary>
        /// Gets the defined templates in the manifests
        /// </summary>
        public IEnumerable<AppletTemplateDefinition> DefinedTemplates
        {
            get => this.m_appletManifest.SelectMany(o => o.Templates);
        }

        /// <summary>
        /// Gets the defined templates 
        /// </summary>
        public IEnumerable<AppletCarePathwayDefinition> DefinedPathways
        {
            get => this.m_appletManifest.SelectMany(o => o.Pathways);
        }

        /// <summary>
        /// Html Assets
        /// </summary>
        public IEnumerable<AppletAsset> HtmlAssets
        {
            get
            {
                if (this.m_htmlAssets == null)
                {
                    this.m_htmlAssets = this.m_appletManifest.SelectMany(m => m.Assets).Where(a => a.MimeType == "text/html").ToList();
                }
                return this.m_htmlAssets;
            }
        }

        /// <summary>
        /// Gets a list of all view states of all loaded applets
        /// </summary>
        public IEnumerable<AppletAsset> ViewStateAssets
        {
            get
            {
                if (m_viewStateAssets == null)
                {
                    m_viewStateAssets = this.HtmlAssets.Where(h => ((h.Content ?? this.Resolver(h)) as AppletAssetHtml)?.ViewState != null).ToList();
                }

                return m_viewStateAssets;
            }
        }

        /// <summary>
        /// Gets all registered dynamic html asset generation source
        /// </summary>
        /// <param name="identifier">The identifier of the dynamic asset extension point</param>
        /// <returns>The list of dynamic applet assets</returns>
        public IEnumerable<AppletAsset> GetDynamicHtmlAssets(String identifier)
        {
            if (!this.m_dynamicHtmlAssets.TryGetValue(identifier, out var dynamicAssetList)) {
                var dynamicIncludeInstructions = this.m_appletManifest
                            .SelectMany(o => o.DynamicHtml.Select(d => new { manifest = o, dhtml = d }))
                            .Where(n => identifier.Equals(n.dhtml.Name, StringComparison.OrdinalIgnoreCase))
                            .SelectMany(o=>o.dhtml.Assets.Select(d => new { manifest = o.manifest, asset = d }));
                dynamicAssetList = this.HtmlAssets.Where(a => dynamicIncludeInstructions?.Any(d => a.Manifest == d.manifest &&  d.asset.Equals(a.Name) || new Regex(d.asset).IsMatch(a.Name)) == true).ToList();
                m_dynamicHtmlAssets.TryAdd(identifier, dynamicAssetList);
            }
            return dynamicAssetList;
        }


        /// <summary>
        /// Gets a list of all widgets for all loaded applets
        /// </summary>
        public IEnumerable<AppletAsset> WidgetAssets
        {
            get
            {
                if (m_widgetAssets == null)
                {
                    this.m_widgetAssets = this
                        .HtmlAssets
                        .Select(o => new { asset = o, content = (this.Resolver != null ? this.Resolver(o) : o.Content) as AppletWidget })
                        .Where(o => o.content != null)
                        .GroupBy(o => o.content.Name)
                        .Select(o => o.OrderByDescending(d => d.content.Priority).First().asset)
                        .ToList();
                }

                return m_widgetAssets;
            }
        }

        /// <summary>
        /// Return true if the collection is readonly
        /// </summary>
        [ExcludeFromCodeCoverage]
        public virtual bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Add an applet manifest to the collection
        /// </summary>
        /// <param name="item"></param>
        public void Add(AppletManifest item)
        {
            if (this.IsReadOnly)
            {
                throw new InvalidOperationException("Collection is readonly");
            }

            this.m_appletManifest.Add(item);
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
            this.ClearCaches();
        }

        /// <summary>
        /// Clear the collection of applets
        /// </summary>
        public void Clear()
        {
            if (this.IsReadOnly)
            {
                throw new InvalidOperationException("Collection is readonly");
            }

            this.m_appletManifest.Clear();
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset, null));
        }


        /// <summary>
        /// Returns true if the collection contains the specified item
        /// </summary>
        [ExcludeFromCodeCoverage]
        public bool Contains(AppletManifest item)
        {
            return this.m_appletManifest.Contains(item);
        }

        /// <summary>
        /// Copies the specified collection to the array
        /// </summary>
        [ExcludeFromCodeCoverage]
        public void CopyTo(AppletManifest[] array, int arrayIndex)
        {
            this.m_appletManifest.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Get the enumerator
        /// </summary>
        public IEnumerator<AppletManifest> GetEnumerator()
        {
            return this.m_appletManifest.GetEnumerator();
        }

        /// <summary>
        /// Get the index of the specified item
        /// </summary>
        [ExcludeFromCodeCoverage]
        public int IndexOf(AppletManifest item)
        {
            return this.m_appletManifest.IndexOf(item);
        }

        /// <summary>
        /// Inserts the specified item at the specified index
        /// </summary>
        public void Insert(int index, AppletManifest item)
        {
            if (this.IsReadOnly)
            {
                throw new InvalidOperationException("Collection is readonly");
            }

            this.m_appletManifest.Insert(index, item);
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        /// <summary>
        /// Remove the specified item from the collection
        /// </summary>
        public bool Remove(AppletManifest item)
        {
            if (this.IsReadOnly)
            {
                throw new InvalidOperationException("Collection is readonly");
            }

            var existingObject = this.m_appletManifest.FirstOrDefault(o => o.Info.Id == item.Info.Id);
            if (existingObject != null)
            {
                this.m_appletManifest.Remove(existingObject);
                this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
            }
            return existingObject != null;
        }

        /// <summary>
        /// Removes the specified item
        /// </summary>
        public void RemoveAt(int index)
        {
            if (this.IsReadOnly)
            {
                throw new InvalidOperationException("Collection is readonly");
            }

            var item = this.m_appletManifest[index];
            this.m_appletManifest.RemoveAt(index);
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
        }

        /// <summary>
        /// Gets the specified enumerator
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.m_appletManifest.GetEnumerator();
        }

        /// <summary>
        /// Register bundle
        /// </summary>
        /// <param name="bundle"></param>
        public void RegisterBundle(RenderBundle bundle)
        {
            if (this.IsReadOnly)
            {
                throw new InvalidOperationException("Collection is readonly");
            }

            this.m_referenceBundles.Add(bundle);
        }

        /// <summary>
        /// Gets the template definition
        /// </summary>
        [Obsolete("Use the IDataTemplateManagerService", true)]
        public AppletTemplateDefinition GetTemplateDefinition(String templateMnemonic)
        {
            AppletTemplateDefinition retVal = null;
            templateMnemonic = templateMnemonic.ToLowerInvariant();
            if (!s_templateCache.TryGetValue(templateMnemonic ?? "", out retVal))
            {
                retVal = this.m_appletManifest
                    .SelectMany(o => o.Templates)
                    .GroupBy(o => o.Mnemonic)
                    .Select(o => o.OrderByDescending(t => t.Priority).FirstOrDefault())
                    .FirstOrDefault(o => o.Mnemonic.ToLowerInvariant() == templateMnemonic);
                s_templateCache.TryAdd(templateMnemonic, retVal);
            }
            return retVal;
        }

        /// <summary>
        /// Gets the template definition
        /// </summary>
        public ViewModel.Description.ViewModelDescription GetViewModelDescription(String viewModelName)
        {
            ViewModelDescription retVal = null;
            viewModelName = viewModelName?.ToLowerInvariant();
            if (!s_viewModelCache.TryGetValue(viewModelName ?? "", out retVal))
            {
                var viewModelDefinition = this.m_appletManifest.SelectMany(o => o.ViewModel).
                    FirstOrDefault(o => o.ViewModelId.ToLowerInvariant() == viewModelName);

                if (viewModelDefinition != null)
                {
                    viewModelDefinition.DefinitionContent = this.RenderAssetContent(this.ResolveAsset(viewModelDefinition.Definition));
                }

                // De-serialize
                if (viewModelDefinition != null)
                {
                    using (MemoryStream ms = new MemoryStream(viewModelDefinition.DefinitionContent))
                    {
                        retVal = ViewModelDescription.Load(ms);
                        foreach (var itm in retVal.Include)
                        {
                            retVal.TypeModelDefinitions.AddRange(this.GetViewModelDescription(itm).TypeModelDefinitions);
                        }

                        // caching
                        if (this.CachePages)
                        {
                            s_viewModelCache.TryAdd(viewModelName, retVal);
                        }
                    }
                }
            }
            return retVal;
        }


        /// <summary>
        /// Get the configured error asset from the applet collection
        /// </summary>
        /// <param name="httpStatusCode">The error for which the applet asset should be retrieved</param>
        /// <returns>The resolved asset (if any)</returns>
        public AppletAsset GetErrorAsset(HttpStatusCode httpStatusCode)
        {
            var assetData = this.SelectMany(o => o.ErrorAssets.Select(e => new { Applet = o, Error = e })).FirstOrDefault(o => o.Error.ErrorCode == (int)httpStatusCode);
            if (assetData == null)
            {
                return null;
            }
            else
            {
                return this.ResolveAsset(assetData.Error.Asset, assetData.Applet);
            }
        }

        /// <summary>
        /// Try to resolve the specified asset
        /// </summary>
        public bool TryResolveApplet(String assetPath, out AppletAsset asset)
        {
            asset = this.ResolveAsset(assetPath);
            return asset != null;
        }


        /// <summary>
        /// Get the configured login asset for this collection
        /// </summary>
        public String GetLoginAssetPath() => this.Select(o => o.LoginAsset).FirstOrDefault();

        /// <summary>
        /// Resolve the asset
        /// </summary>
        public AppletAsset ResolveAsset(String assetPath, AppletManifest relativeManifest = null, AppletAsset relativeAsset = null)
        {
            if (assetPath == null)
            {
                return null;
            }

            // Manifest to search for asset
            AppletManifest searchManifest = null;

            // Is the asset start with ~
            if (assetPath.StartsWith("/"))
            { // Absolute
                var pathRegex = new Regex(@"^\/(.*?)\/(.*)$");
                var pathData = pathRegex.Match(assetPath);
                if (pathData.Success)
                {
                    searchManifest = this.FirstOrDefault(o => o.Info.Id == pathData.Groups[1].Value);
                    assetPath = pathData.Groups[2].Value;
                }
                else
                {
                    throw new InvalidCastException("Absolute references must be in format /id.to.the.applet/path/to/the/file");
                }
            }
            else if (assetPath.StartsWith("~"))
            {
                assetPath = assetPath.Substring(2); // it is in current path
                searchManifest = relativeManifest ?? relativeAsset?.Manifest;
                if (searchManifest == null)
                {
                    throw new InvalidOperationException("Cannot search relative manifest with no reference/related asset");
                }
            }
            else
            {
                searchManifest = relativeManifest ?? relativeAsset?.Manifest;
            }

            if (assetPath.EndsWith("/") || String.IsNullOrEmpty(assetPath))
            {
                assetPath += "index.html";
            }

            //assetPath = assetPath.ToLower(); // case insensitive
            return searchManifest?.Assets.FirstOrDefault(o => o.Name == assetPath);
        }

        /// <summary>
        /// Render asset content
        /// </summary>
        public byte[] RenderAssetContent(AppletAsset asset, string preProcessLocalization = null, bool staticScriptRefs = true, bool allowCache = true, IDictionary<String, String> bindingParameters = null)
        {
            // TODO: This method needs to be cleaned up since it exists from the old/early OpenIZ days
            // First, is there an object already
            byte[] cacheObject = null;
            string assetPath = String.Format("{0}?lang={1}", asset.ToString(), preProcessLocalization);

            var cacheKey = $"{assetPath};{String.Join(";", bindingParameters?.Select(o => $"{o.Key}={o.Value}") ?? new string[0] { })}";
            if (allowCache && this.CachePages && s_cache.TryGetValue(cacheKey, out cacheObject))
            {
                return cacheObject;
            }

            // Resolve content
            var content = asset.Content;
            if (this.Resolver != null)
            {
                content = this.Resolver(asset);
            }

            switch (content)
            {
                case String str:
                    if (asset.MimeType == "text/javascript" || asset.MimeType == "application/json")
                    {
                        if (bindingParameters != null)
                        {
                            str = this.m_bindingRegex.Replace(str, (m) => bindingParameters.TryGetValue(m.Groups[1].Value, out string v) ? v : m.ToString());
                        }

                        cacheObject = Encoding.UTF8.GetBytes(str);
                        if (allowCache)
                        {
                            s_cache.TryAdd(cacheKey, cacheObject);
                        }
                        return cacheObject;
                    }
                    else
                    {
                        return Encoding.UTF8.GetBytes(str);
                    }
                case byte[] bytea:
                    return bytea;
                case AppletAssetHtml html:
                    // Clone the asset HTML so we can manipulate it for this locale without messing up the original
                    var htmlAsset = new AppletAssetHtml()
                    {
                        Html = new XElement(html.Html),
                        Script = new List<AssetScriptReference>(html.Script),
                        Titles = new List<LocaleString>(html.Titles),
                        Style = new List<string>(html.Style)
                    };
                    XElement htmlContent = htmlAsset.Html;

                    if (!htmlAsset.Static)
                    {
                        // Type of tag to render basic content
                        switch (htmlAsset.Html.Name.LocalName)
                        {
                            case "html": // The content is a complete HTML page
                                {
                                    htmlContent = htmlAsset.Html;
                                    var headerInjection = this.GetInjectionHeaders(asset, htmlContent.DescendantNodes().OfType<XElement>().Any(o => o.Name == xs_xhtml + "ui-view"));

                                    // STRIP - SanteDBJS references
                                    var head = htmlContent.DescendantNodes().OfType<XElement>().FirstOrDefault(o => o.Name == xs_xhtml + "head");
                                    if (head == null)
                                    {
                                        head = new XElement(xs_xhtml + "head");
                                        htmlContent.Add(head);
                                    }

                                    head.Add(headerInjection.OfType<XElement>().Where(o => !head.Elements(o.Name).Any(e => (e.Attributes("src") != null && (e.Attributes("src") == o.Attributes("src"))) || (e.Attributes("href") != null && (e.Attributes("href") == o.Attributes("href"))))));
                                    head.Add(headerInjection.OfType<XComment>());

                                    // Inject any business rules as static refs
                                    var body = htmlContent.DescendantNodes().OfType<XElement>().FirstOrDefault(o => o.Name == xs_xhtml + "body");
                                    if (body != null)
                                    {
                                        body.Add(
                                            this.SelectMany(o => o.Assets.Where(a => a.Name.StartsWith("rules/"))).Select(o => new XElement(xs_xhtml + "script", new XAttribute("src", $"/{o.Manifest.Info.Id}/{o.Name}"), new XAttribute("type", "text/javascript"), new XAttribute("nonce", bindingParameters.TryGetValue("csp_nonce", out string nonce) ? nonce : ""), new XText("// Script reference")))
                                        );
                                    }
                                    break;
                                }
                            case "body": // The content is an HTML Body element, we must inject the HTML header
                                {
                                    htmlContent = htmlAsset.Html;

                                    // Inject special headers
                                    var headerInjection = this.GetInjectionHeaders(asset, htmlContent.DescendantNodes().OfType<XElement>().Any(o => o.Name == xs_xhtml + "ui-view"));

                                    // Render the bundles
                                    var bodyElement = htmlAsset.Html as XElement;
                                    htmlContent = new XElement(xs_xhtml + "html", new XAttribute("ng-app", asset.Name), new XElement(xs_xhtml + "head", headerInjection), bodyElement);
                                }
                                break;
                            case "div":
                                {

                                    if (htmlAsset.Script.Any() && content is AppletWidget)
                                    {
                                        htmlContent = htmlAsset.Html;
                                        // Render out oc-lazy-load
                                        var lazyLoadName = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(asset.Name)).HexEncode();
                                        var resolvedScripts = htmlAsset.Script.Select(o =>
                                            o.Reference.StartsWith("~") ? String.Format("/{0}/{1}", asset.Manifest.Info.Id, o.Reference.Substring(2)) : o.Reference
                                        );

                                        var lazyLoadAttribute = new XAttribute("oc-lazy-load", $"{{ name: '{lazyLoadName}', files: [ {String.Join(",", resolvedScripts.Select(s => $"'{s}'"))} ] }}");
                                        var bodyElement = htmlAsset.Html as XElement;
                                        htmlContent = new XElement(xs_xhtml + "div", lazyLoadAttribute, bodyElement);
                                    }
                                    break;
                                }
                        } // switch

                        // Now process SSI directives - <!--#include virtual="XXXXXXX" -->
                        var includes = htmlContent.DescendantNodes().OfType<XComment>().Where(o => o?.Value?.Trim().StartsWith("#include virtual=\"") == true).ToList();
                        foreach (var inc in includes)
                        {
                            String assetName = inc.Value.Trim().Substring(18); // HACK: Should be a REGEX
                            if (assetName.EndsWith("\""))
                            {
                                assetName = assetName.Substring(0, assetName.Length - 1);
                            }

                            if (assetName == "content")
                            {
                                continue;
                            }

                            var includeAsset = this.ResolveAsset(assetName, relativeAsset: asset);
                            if (includeAsset == null)
                            {
                                inc.AddAfterSelf(new XElement(xs_xhtml + "strong", new XText(String.Format("{0} NOT FOUND", assetName))));
                                inc.Remove();
                            }
                            else
                            {
                                using (MemoryStream ms = new MemoryStream(this.RenderAssetContent(includeAsset, preProcessLocalization, bindingParameters: bindingParameters)))
                                {
                                    try
                                    {
                                        var xel = XDocument.Load(ms).Elements().First() as XElement;
                                        if (xel.Name == xs_xhtml + "html")
                                        {
                                            inc.AddAfterSelf(xel.Element(xs_xhtml + "body").Elements());
                                        }
                                        else
                                        {
                                            //var headerInjection = this.GetInjectionHeaders(includeAsset);

                                            //var headElement = htmlContent.Element(xs_xhtml + "head");
                                            //headElement?.Add(headerInjection.Where(o => !headElement.Elements(o.Name).Any(e => (e.Attributes("src") != null && (e.Attributes("src") == o.Attributes("src"))) || (e.Attributes("href") != null && (e.Attributes("href") == o.Attributes("href"))))));

                                            inc.AddAfterSelf(xel);
                                        }
                                        inc.Remove();
                                    }
                                    catch (Exception e)
                                    {
                                        throw new XmlException($"Error in Asset: {includeAsset}", e);
                                    }
                                }
                            }
                        }

                        // Re-write
                        foreach (var itm in htmlContent.DescendantNodes().OfType<XElement>().SelectMany(o => o.Attributes()).Where(o => o.Value.StartsWith("~")))
                        {
                            itm.Value = String.Format("/{0}/{1}", asset.Manifest.Info.Id, itm.Value.Substring(2));
                            //itm.Value = itm.Value.Replace(APPLET_SCHEME, this.AppletBase).Replace(ASSET_SCHEME, this.AssetBase).Replace(DRAWABLE_SCHEME, this.DrawableBase);
                        }

                        // Render Title
                        var headTitle = htmlContent.DescendantNodes().OfType<XElement>().FirstOrDefault(o => o.Name == xs_xhtml + "head");
                        var title = htmlAsset.GetTitle(preProcessLocalization);
                        if (headTitle != null && !String.IsNullOrEmpty(title))
                        {
                            headTitle.Add(new XElement(xs_xhtml + "title", new XText(title)));
                        }
                    }

                    // Render out the content
                    using (StringWriter sw = new StringWriter())
                    using (XmlWriter xw = XmlWriter.Create(sw, new XmlWriterSettings() { OmitXmlDeclaration = true }))
                    {
                        htmlContent.WriteTo(xw);
                        xw.Flush();

                        String retVal = sw.ToString();
                        if (!String.IsNullOrEmpty(preProcessLocalization))
                        {
                            var localizationService = ApplicationServiceContext.Current.GetService<ILocalizationService>();

                            retVal = this.m_localizationRegex.Replace(retVal, (m) => localizationService?.GetString(preProcessLocalization, m.Groups[1].Value) ?? m.Groups[1].Value);
                        }

                        // Binding objects
                        if (bindingParameters != null)
                        {
                            retVal = this.m_bindingRegex.Replace(retVal, (m) => bindingParameters.TryGetValue(m.Groups[1].Value, out string v) ? v : m.ToString());
                        }

                        var byteData = Encoding.UTF8.GetBytes(retVal);
                        // Add to cache
                        if (allowCache)
                        {
                            s_cache.TryAdd(cacheKey, byteData);
                        }

                        return byteData;
                    }
                case AppletAssetVirtual virtualContent:
                    var renderedAsset = virtualContent.Include.SelectMany(includePath =>
                    {
                        var regExp = new Regex(includePath);
                        return asset.Manifest.Assets.Where(o => regExp.IsMatch(o.Name)).SelectMany(inclAsset => this.RenderAssetContent(inclAsset, preProcessLocalization, staticScriptRefs, allowCache, bindingParameters));
                    }).ToArray();

                    if (allowCache)
                    {
                        s_cache.TryAdd(cacheKey, renderedAsset);
                    }
                    return renderedAsset;
                default:
                    return null;
            }


        }

        /// <summary>
        /// Rewrte the url
        /// </summary>
        private string RewriteUrl(Uri appletUri)
        {
            Uri rewrite = new Uri(this.m_baseUrl);
            return String.Format("{0}/{1}/{2}", rewrite, appletUri.Host, appletUri.PathAndQuery);
        }


        /// <summary>
        /// Injection for HTML headers
        /// </summary>
        public List<AssetScriptReference> GetLazyScripts(AppletAsset asset)
        {
            var htmlAsset = asset.Content as AppletAssetHtml;
            if (this.Resolver != null)
            {
                htmlAsset = this.Resolver(asset) as AppletAssetHtml;
            }

            // Insert scripts & Styles
            List<AssetScriptReference> scriptRefs = new List<AssetScriptReference>();
            if (htmlAsset == null)
            {
                return scriptRefs;
            }

            scriptRefs.AddRange(htmlAsset.Script.Where(o => o.IsStatic == false));

            // Content - SSI
            var includes = htmlAsset.Html.DescendantNodes().OfType<XComment>().Where(o => o?.Value?.Trim().StartsWith("#include virtual=\"") == true).ToList();
            foreach (var inc in includes)
            {
                String assetName = inc.Value.Trim().Substring(18); // HACK: Should be a REGEX
                if (assetName.EndsWith("\""))
                {
                    assetName = assetName.Substring(0, assetName.Length - 1);
                }

                if (assetName == "content")
                {
                    continue;
                }

                var includeAsset = this.ResolveAsset(assetName, relativeAsset: asset);
                if (includeAsset != null)
                {
                    scriptRefs.AddRange(this.GetLazyScripts(includeAsset));
                }
            }

            // Re-write
            foreach (var itm in scriptRefs.Where(o => o.Reference.StartsWith("~")))
            {
                itm.Reference = String.Format("/{0}/{1}", asset.Manifest.Info.Id, itm.Reference.Substring(2));
                //itm.Value = itm.Value.Replace(APPLET_SCHEME, this.AppletBase).Replace(ASSET_SCHEME, this.AssetBase).Replace(DRAWABLE_SCHEME, this.DrawableBase);
            }
            return scriptRefs.Distinct(new AssetScriptReferenceEqualityComparer()).ToList();
        }

        /// <summary>
        /// Injection for HTML headers
        /// </summary>
        private List<XNode> GetInjectionHeaders(AppletAsset asset, bool isUiContainer)
        {
            var htmlAsset = asset.Content as AppletAssetHtml;
            if (this.Resolver != null)
            {
                htmlAsset = this.Resolver(asset) as AppletAssetHtml;
            }

            // Insert scripts & Styles
            List<XNode> headerInjection = new List<XNode>();
            if (htmlAsset == null)
            {
                return headerInjection;
            }

            // Inject special headers
            foreach (var itm in htmlAsset.Bundle)
            {
                var bundle = this.m_referenceBundles.Find(o => o.Name == itm);
                if (bundle == null)
                {
                    throw new FileNotFoundException(String.Format("Bundle {0} not found", itm));
                }

                headerInjection.AddRange(bundle.Content.SelectMany(o => o.HeaderElement));
            }

            // All scripts
            if (isUiContainer) // IS A UI CONTAINER = ANGULAR UI REQUIRES ALL CONTROLLERS BE LOADED
            {
                return this.ViewStateAssets.SelectMany(o => this.GetInjectionHeaders(o, false)).Distinct(new XNodeEquityComparer()).ToList();
            }
            else
            {
                foreach (var itm in htmlAsset.Script.Where(o => o.IsStatic != false))
                {
                    var incAsset = this.ResolveAsset(itm.Reference, relativeAsset: asset);
                    if (incAsset != null)
                    {
                        headerInjection.AddRange(new ScriptBundleContent(itm.Reference).HeaderElement);
                    }
                    else
                    {
                        headerInjection.Add(new XComment($"Asset {itm.Reference} not found"));
                    }
                }
            }

            foreach (var itm in htmlAsset.Style)
            {
                var incAsset = this.ResolveAsset(itm, relativeAsset: asset);
                if (incAsset != null)
                {
                    headerInjection.AddRange(new StyleBundleContent(itm).HeaderElement);
                }
            }

            // Content - SSI
            var includes = htmlAsset.Html.DescendantNodes().OfType<XComment>().Where(o => o?.Value?.Trim().StartsWith("#include virtual=\"") == true).ToList();
            foreach (var inc in includes)
            {
                String assetName = inc.Value.Trim().Substring(18); // HACK: Should be a REGEX
                if (assetName.EndsWith("\""))
                {
                    assetName = assetName.Substring(0, assetName.Length - 1);
                }

                if (assetName == "content")
                {
                    continue;
                }

                var includeAsset = this.ResolveAsset(assetName, relativeAsset: asset);
                if (includeAsset != null)
                {
                    headerInjection.AddRange(this.GetInjectionHeaders(includeAsset, isUiContainer));
                }
            }

            // Re-write
            foreach (var itm in headerInjection.OfType<XElement>().SelectMany(o => o.Attributes()).Where(o => o.Value.StartsWith("~")))
            {
                itm.Value = String.Format("/{0}/{1}", asset.Manifest.Info.Id, itm.Value.Substring(2));
                //itm.Value = itm.Value.Replace(APPLET_SCHEME, this.AppletBase).Replace(ASSET_SCHEME, this.AssetBase).Replace(DRAWABLE_SCHEME, this.DrawableBase);
            }
            return headerInjection.Distinct(new XNodeEquityComparer()).ToList();
        }

        /// <summary>
        /// Verify dependencies are met for the specified applet
        /// </summary>
        public bool VerifyDependencies(AppletInfo applet)
        {
            bool verified = true;
            foreach (var itm in applet.Dependencies)
            {
                var depItm = this.m_appletManifest.FirstOrDefault(o => o.Info.Id == itm.Id && (o.Info.PublicKeyToken == itm.PublicKeyToken || String.IsNullOrEmpty(itm.PublicKeyToken)));
                if (depItm == null)
                {
                    return false;
                }
                else
                {
                    verified &= depItm != null && depItm?.Info.Version.ParseVersion(out _) >= (itm.Version ?? "0.0.0.0").ParseVersion(out _);
                }
            }
            return verified;
        }

        /// <summary>
        /// Readonly applet collection
        /// </summary>
        public ReadonlyAppletCollection AsReadonly()
        {
            return new ReadonlyAppletCollection(this);
        }

        /// <summary>
        /// Asset script reference comparer
        /// </summary>
        private class AssetScriptReferenceEqualityComparer : IEqualityComparer<AssetScriptReference>
        {
            /// <summary>
            /// Equality comparer
            /// </summary>
            public bool Equals(AssetScriptReference x, AssetScriptReference y)
            {
                return x.Reference?.Equals(y.Reference) == true;
            }

            /// <summary>
            /// Get hash code
            /// </summary>
            public int GetHashCode(AssetScriptReference obj)
            {
                return obj.Reference.GetHashCode();
            }
        }

        /// <summary>
        /// Xelement comparer
        /// </summary>
        private class XNodeEquityComparer : IEqualityComparer<XNode>
        {
            public bool Equals(XNode x, XNode y)
            {
                if (x.GetType() != y.GetType())
                {
                    return false;
                }
                else if (x is XElement xE && y is XElement yE)
                {

                    bool equals = true;
                    foreach (var xa in xE.Attributes())
                    {
                        var ya = yE.Attribute(xa.Name);
                        equals &= xa.Value == ya?.Value;
                    }
                    return equals;
                }
                else
                    return x.ToString().Equals(y.ToString());
            }

            /// <summary>
            /// Get Hash code
            /// </summary>
            public int GetHashCode(XNode obj)
            {
                if (obj is XElement xe)
                {
                    return xe.Attribute("src")?.Value.GetHashCode() ?? xe.Attribute("href")?.Value.GetHashCode() ?? obj.GetHashCode();
                }
                return obj.GetHashCode();
            }
        }

        /// <summary>
        /// Get the template instance with the specified parameters
        /// </summary>
        /// <param name="templateId">The identifier of the template to fetch</param>
        /// <param name="parameters">The parameters to use</param>
        [Obsolete("Use the IDataTemplateManagerService", true)]
        public IdentifiedData GetTemplateInstance(string templateId, IDictionary<String, String> parameters = null)
        {
            var definition = this.GetTemplateDefinition(templateId);
            if (definition == null)
            {
                throw new FileNotFoundException($"Template {templateId} not found");
            }

            var definitionAsset = this.ResolveAsset(definition.Definition);
            if (definitionAsset == null)
            {
                throw new FileNotFoundException($"Template content {definition.Definition} not found");
            }

            parameters = parameters ?? new Dictionary<String, String>();
            parameters.Add("today", DateTimeOffset.Now.Date.ToString("yyyy-MM-dd"));
            parameters.Add("now", DateTimeOffset.Now.ToString("o"));

            using (var ms = new MemoryStream(this.RenderAssetContent(definitionAsset, bindingParameters: parameters, allowCache: false)))
            using (var json = new JsonViewModelSerializer())
            {
                var result = json.DeSerialize<IdentifiedData>(ms);
                if (result is IHasTemplate template) // Correct any type-os in the JSON
                {
                    template.Template = new TemplateDefinition() { Key = definition.Uuid, Description = definition.Description, Mnemonic = templateId };
                    template.TemplateKey = definition.Uuid;
                }

                return result;
            }
        }
    }
}