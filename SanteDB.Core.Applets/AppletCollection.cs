﻿/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.ViewModel.Description;
using SanteDB.Core.Applets.ViewModel.Json;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Interfaces;
using SharpCompress.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
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
            wrap.CollectionChanged += (o, e) => this.CollectionChanged?.Invoke(o, e);
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
        private static Dictionary<String, Byte[]> s_cache = new Dictionary<string, byte[]>();
        private static Dictionary<String, List<KeyValuePair<String, String>>> s_stringCache = new Dictionary<string, List<KeyValuePair<string, string>>>();
        private static Dictionary<String, AppletTemplateDefinition> s_templateCache = new Dictionary<string, AppletTemplateDefinition>();
        private static Dictionary<String, ViewModelDescription> s_viewModelCache = new Dictionary<string, ViewModelDescription>();
        private static List<AppletAsset> s_viewStateAssets = null;
        private static List<AppletAsset> s_widgetAssets = null;

        private static Object s_syncLock = new object();

        private AssetContentResolver m_resolver = null;
        private Regex m_localizationRegex = new Regex("{{\\s?:?:?'([A-Za-z0-9\\._\\-]*?)'\\s?\\|\\s?i18n\\s?}}");
        private Regex m_bindingRegex = new Regex("{{\\s?\\$([A-Za-z0-9_]*?)\\s?}}");

        private Tracer m_tracer = Tracer.GetTracer(typeof(AppletCollection));

        /// <summary>
        /// Represetns the applet scheme
        /// </summary>
        public const string APPLET_SCHEME = "app://";

        private string m_baseUrl = null;
        private bool m_cachePages = true;

        // XMLNS stuff
        private readonly XNamespace xs_xhtml = "http://www.w3.org/1999/xhtml";
        private readonly XNamespace xs_binding = "http://santedb.org/applet/binding";

        /// <summary>
        /// Gets or sets whether caching is enabled
        /// </summary>
        public virtual Boolean CachePages { get { return this.m_cachePages; } set { this.m_cachePages = value; } }

        /// <summary>
        /// Gets or sets the base url
        /// </summary>
        public virtual String BaseUrl
        {
            get { return this.m_baseUrl; }
            set
            {
                if (this.IsReadOnly)
                    throw new InvalidOperationException("Collection is readonly");
                this.m_baseUrl = value;
            }
        }

        /// <summary>
        /// Asset content resolver called when asset content is null
        /// </summary>
        public virtual AssetContentResolver Resolver
        {
            get { return this.m_resolver; }
            set
            {
                if (this.IsReadOnly)
                    throw new InvalidOperationException("Collection is readonly");
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
            AppletCollection.ClearCaches();
        }

        /// <summary>
        /// Applet collection rewrite to alternate url
        /// </summary>
        public AppletCollection(String baseUrl)
        {
            this.m_baseUrl = baseUrl;
            AppletCollection.ClearCaches();

        }

        /// <summary>
        /// Clear all caches
        /// </summary>
        public static void ClearCaches()
        {
            s_stringCache?.Clear();
            s_viewStateAssets?.Clear();
            s_widgetAssets?.Clear();
            s_viewModelCache?.Clear();
            s_templateCache?.Clear();
            s_cache?.Clear();
            s_viewStateAssets = null;
            s_widgetAssets = null;
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
        public int Count
        {
            get
            {
                return this.m_appletManifest.Count;
            }
        }

        /// <summary>
        /// Gets a list of all view states of all loaded applets
        /// </summary>
        public List<AppletAsset> ViewStateAssets
        {
            get
            {
                if (s_viewStateAssets == null)
                    s_viewStateAssets = this.m_appletManifest.SelectMany(m => m.Assets).Where(a => ((a.Content == null && this.Resolver != null ? this.Resolver(a) : a.Content) as AppletAssetHtml)?.ViewState != null).ToList();
                return s_viewStateAssets;
            }
        }

        /// <summary>
        /// Gets a list of all widgets for all loaded applets
        /// </summary>
        public List<AppletAsset> WidgetAssets
        {
            get
            {
                if (s_widgetAssets == null)
                    s_widgetAssets = this.m_appletManifest.SelectMany(m => m.Assets)
                        .Where(o => o.MimeType == "text/html")
                        .Select(o => new { asset = o, content = (o.Content == null && this.Resolver != null ? this.Resolver(o) : o.Content) as AppletWidget })
                        .Where(o => o.content != null)
                        .GroupBy(o => o.content.Name)
                        .Select(o => o.OrderByDescending(d => d.content.Priority).First().asset)
                        .ToList();
                return s_widgetAssets;
            }
        }


        /// <summary>
        /// Return true if the collection is readonly
        /// </summary>
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
            if (this.IsReadOnly) throw new InvalidOperationException("Collection is readonly");
            this.m_appletManifest.Add(item);
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
            AppletCollection.ClearCaches();
        }

        /// <summary>
        /// Clear the collection of applets
        /// </summary>
        public void Clear()
        {
            if (this.IsReadOnly) throw new InvalidOperationException("Collection is readonly");

            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset, this.m_appletManifest));
            this.m_appletManifest.Clear();

        }

        /// <summary>
        /// Returns true if the collection contains the specified item
        /// </summary>
        public bool Contains(AppletManifest item)
        {
            return this.m_appletManifest.Contains(item);
        }

        /// <summary>
        /// Copies the specified collection to the array
        /// </summary>
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
        public int IndexOf(AppletManifest item)
        {
            return this.m_appletManifest.IndexOf(item);
        }

        /// <summary>
        /// Inserts the specified item at the specified index
        /// </summary>
        public void Insert(int index, AppletManifest item)
        {
            if (this.IsReadOnly) throw new InvalidOperationException("Collection is readonly");
            this.m_appletManifest.Insert(index, item);
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));

        }

        /// <summary>
        /// Remove the specified item from the collection
        /// </summary>
        public bool Remove(AppletManifest item)
        {
            if (this.IsReadOnly) throw new InvalidOperationException("Collection is readonly");

            var retVal = this.m_appletManifest.Remove(item);
            if (retVal) this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
            return retVal;
        }

        /// <summary>
        /// Removes the specified item
        /// </summary>
        public void RemoveAt(int index)
        {
            if (this.IsReadOnly) throw new InvalidOperationException("Collection is readonly");

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
            if (this.IsReadOnly) throw new InvalidOperationException("Collection is readonly");

            this.m_referenceBundles.Add(bundle);
        }

        /// <summary>
        /// Get the list of strings from all the loaded applets
        /// </summary>
        public List<KeyValuePair<String, String>> GetStrings(String locale)
        {
            List<KeyValuePair<String, String>> retVal = null;
            if (!s_stringCache.TryGetValue(locale ?? "", out retVal))
                lock (s_syncLock)
                {
                    if (!s_stringCache.TryGetValue(locale ?? "", out retVal))
                    {
                        retVal = this.m_appletManifest.SelectMany(o => o.Strings).
                        Where(o => o.Language == locale).
                        SelectMany(o => o.String).
                        GroupBy(s => s.Key).
                        Select(o => new KeyValuePair<String, String>(o.Key, o.OrderByDescending(s=>s.Priority).FirstOrDefault().Value)).ToList();
                        s_stringCache.Add(locale, retVal);
                    }
                }
            return retVal;
        }

        /// <summary>
        /// Gets the template definition
        /// </summary>
        public AppletTemplateDefinition GetTemplateDefinition(String templateMnemonic)
        {
            AppletTemplateDefinition retVal = null;
            templateMnemonic = templateMnemonic.ToLowerInvariant();
            if (!s_templateCache.TryGetValue(templateMnemonic ?? "", out retVal))
                lock (s_syncLock)
                {
                    retVal = this.m_appletManifest
                        .SelectMany(o => o.Templates)
                        .GroupBy(o=>o.Mnemonic)
                        .Select(o=>o.OrderByDescending(t=>t.Priority).FirstOrDefault())
                        .FirstOrDefault(o => o.Mnemonic.ToLowerInvariant() == templateMnemonic);
                    s_templateCache.Add(templateMnemonic, retVal);
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
                lock (s_syncLock)
                {
                    var viewModelDefinition = this.m_appletManifest.SelectMany(o => o.ViewModel).
                        FirstOrDefault(o => o.ViewModelId.ToLowerInvariant() == viewModelName);

                    if (viewModelDefinition != null)
                        viewModelDefinition.DefinitionContent = this.RenderAssetContent(this.ResolveAsset(viewModelDefinition.Definition));

                    // De-serialize
                    if (viewModelDefinition != null)
                        using (MemoryStream ms = new MemoryStream(viewModelDefinition.DefinitionContent))
                        {
                            retVal = ViewModelDescription.Load(ms);
                            foreach (var itm in retVal.Include)
                                retVal.Model.AddRange(this.GetViewModelDescription(itm).Model);

                            // caching 
                            if (this.CachePages)
                                if (!s_viewModelCache.ContainsKey(viewModelName))
                                    s_viewModelCache.Add(viewModelName, retVal);
                        }

                }
            return retVal;
        }


        /// <summary>
        /// Resolve the asset 
        /// </summary>
        public AppletAsset ResolveAsset(String assetPath, AppletAsset relative = null, String language = null)
        {

            if (assetPath == null)
                return null;

            // Is the asset start with ~
            if (assetPath.StartsWith("~"))
                assetPath = "/" + relative.Manifest.Info.Id + assetPath.Substring(1);

            Uri path = null;
            if (!Uri.TryCreate(assetPath, UriKind.RelativeOrAbsolute, out path))
                return null;
            else
            {

                AppletManifest resolvedManifest = null;
                String pathLeft = path.IsAbsoluteUri ? path.AbsolutePath.Substring(1) :
                    path.OriginalString.StartsWith("/") ? path.OriginalString.Substring(1) : path.OriginalString;
                // Is the host specified?
                if (path.IsAbsoluteUri && !String.IsNullOrEmpty(path.Host))
                {
                    resolvedManifest = this.FirstOrDefault(o => o.Info.Id == path.Host);
                }
                else
                {
                    // We can accept /org.x.y.z or /org/x/y/z
                    StringBuilder applId = new StringBuilder();
                    while (pathLeft.Contains("/"))
                    {
                        applId.AppendFormat("{0}.", pathLeft.Substring(0, pathLeft.IndexOf("/")));
                        pathLeft = pathLeft.Substring(pathLeft.IndexOf("/") + 1);
                        resolvedManifest = this.FirstOrDefault(o => o.Info.Id == applId.ToString(0, applId.Length - 1));
                        if (resolvedManifest != null) break;
                    }
                }
                if (resolvedManifest == null) resolvedManifest = relative?.Manifest;

                // Is there a resource?
                if (resolvedManifest != null)
                {
                    if (pathLeft.EndsWith("/") || String.IsNullOrEmpty(pathLeft))
                        pathLeft += "index.html";
                    pathLeft = pathLeft.ToLower(); // case insensitive
                    return resolvedManifest.Assets.FirstOrDefault(o => o.Name == pathLeft);
                }


                return null;
            }
        }

        /// <summary>
        /// Render asset content
        /// </summary>
        public byte[] RenderAssetContent(AppletAsset asset, string preProcessLocalization = null, bool staticScriptRefs = true, bool allowCache = true, IDictionary<String, String> bindingParameters = null)
        {

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
            if (content == null && this.Resolver != null)
                content = this.Resolver(asset);

            if (content is String) // Content is a string
            {

                // Inject CSP 
                if (asset.MimeType == "text/javascript" || asset.MimeType == "application/json")
                {
                    var retVal = content as String;
                    if (bindingParameters != null)
                        retVal = this.m_bindingRegex.Replace(retVal, (m) => bindingParameters.TryGetValue(m.Groups[1].Value, out string v) ? v : m.ToString());
                    cacheObject = Encoding.UTF8.GetBytes(retVal);
                    lock (s_syncLock)
                        if (allowCache && !s_cache.ContainsKey(cacheKey))
                            s_cache.Add(cacheKey, cacheObject);
                    return cacheObject;
                }
                else
                    return Encoding.UTF8.GetBytes(content as String);
            }
            else if (content is byte[]) // Content is a binary asset 
            {
                // is the content compressed? 
                if (Encoding.UTF8.GetString(content as byte[], 0, 4) == "LZIP")
                {
                    using (var ms = new MemoryStream(content as byte[]))
                    using (var ls = new SharpCompress.Compressors.LZMA.LZipStream(new NonDisposingStream(ms), SharpCompress.Compressors.CompressionMode.Decompress))
                    using (var oms = new MemoryStream())
                    {
                        byte[] buffer = new byte[2048];
                        int br = 1;
                        while (br > 0)
                        {
                            br = ls.Read(buffer, 0, 2048);
                            oms.Write(buffer, 0, br);
                        }

                        content = oms.ToArray();
                        lock (s_cache)
                            if (!s_cache.ContainsKey(cacheKey))
                                s_cache.Add(cacheKey, content as byte[]);
                        return content as byte[];
                    }
                }
                else
                    return content as byte[];
            }
            else if (content is XElement) // Content is XML
            {
                using (MemoryStream ms = new MemoryStream())
                using (XmlWriter xw = XmlWriter.Create(ms))
                {
                    (content as XElement).WriteTo(xw);
                    xw.Flush();
                    ms.Flush();
                    return ms.ToArray();
                }
            }
            else if (content is AppletAssetHtml) // Content is HTML
            {
                // Is the content HTML?
                var sourceAsset = content as AppletAssetHtml;
                var htmlAsset = new AppletAssetHtml()
                {
                    Html = new XElement(sourceAsset.Html),
                    Layout = sourceAsset.Layout,
                    Script = new List<AssetScriptReference>(sourceAsset.Script),
                    Titles = new List<LocaleString>(sourceAsset.Titles),
                    Style = new List<string>(sourceAsset.Style)
                };
                XElement htmlContent = null;

                if (htmlAsset.Static)
                    htmlContent = htmlAsset.Html as XElement;
                else
                {
                    // Type of tag to render basic content
                    switch (htmlAsset.Html.Name.LocalName)
                    {
                        case "html": // The content is a complete HTML page
                            {
                                htmlContent = htmlAsset.Html as XElement;
                                var headerInjection = this.GetInjectionHeaders(asset, htmlContent.DescendantNodes().OfType<XElement>().Any(o => o.Name == xs_xhtml + "ui-view"));

                                // STRIP - SanteDBJS references
                                var xel = htmlContent.Descendants().OfType<XElement>().Where(o => o.Name == xs_xhtml + "script" && o.Attribute("src")?.Value.Contains("SanteDB") == true).ToArray();
                                var head = htmlContent.DescendantNodes().OfType<XElement>().FirstOrDefault(o => o.Name == xs_xhtml + "head");
                                if (head == null)
                                {
                                    head = new XElement(xs_xhtml + "head");
                                    htmlContent.Add(head);
                                }

                                head.Add(headerInjection.Where(o => !head.Elements(o.Name).Any(e => (e.Attributes("src") != null && (e.Attributes("src") == o.Attributes("src"))) || (e.Attributes("href") != null && (e.Attributes("href") == o.Attributes("href"))))));

                                // Inject any business rules as static refs
                                var body = htmlContent.DescendantNodes().OfType<XElement>().FirstOrDefault(o => o.Name == xs_xhtml + "body");
                                if (body != null)
                                {
                                    body.Add(
                                        this.SelectMany(o => o.Assets.Where(a => a.Name.StartsWith("rules/"))).Select(o => new XElement(xs_xhtml + "script", new XAttribute("src", $"/{o.Manifest.Info.Id}/{o.Name}"), new XAttribute("type", "text/javascript"), new XAttribute("nonce", bindingParameters.TryGetValue("csp_nonce", out string nonce) ? nonce : ""), new XText("// Script reference")))
                                    );
                                }
                                //                            head.Add(headerInjection);
                                break;
                            }
                        case "body": // The content is an HTML Body element, we must inject the HTML header
                            {
                                htmlContent = htmlAsset.Html as XElement;

                                // Inject special headers
                                var headerInjection = this.GetInjectionHeaders(asset, htmlContent.DescendantNodes().OfType<XElement>().Any(o => o.Name == xs_xhtml + "ui-view"));

                                // Render the bundles
                                var bodyElement = htmlAsset.Html as XElement;

                                htmlContent = new XElement(xs_xhtml + "html", new XAttribute("ng-app", asset.Name), new XElement(xs_xhtml + "head", headerInjection), bodyElement);
                            }
                            break;
                        default:
                            {
                                if (String.IsNullOrEmpty(htmlAsset.Layout))
                                    htmlContent = htmlAsset.Html as XElement;
                                else
                                {


                                    // Get the layout
                                    var layoutAsset = this.ResolveAsset(htmlAsset.Layout, asset);
                                    if (layoutAsset == null)
                                        throw new FileNotFoundException(String.Format("Layout asset {0} not found", htmlAsset.Layout));

                                    using (MemoryStream ms = new MemoryStream(this.RenderAssetContent(layoutAsset, preProcessLocalization, bindingParameters: bindingParameters)))
                                        htmlContent = XDocument.Load(ms).FirstNode as XElement;


                                    // Find the <!--#include virtual="content" --> tag
                                    var contentNode = htmlContent.DescendantNodes().OfType<XComment>().SingleOrDefault(o => o.Value.Trim() == "#include virtual=\"content\"");
                                    if (contentNode != null)
                                    {
                                        contentNode.AddAfterSelf(htmlAsset.Html as XElement);
                                        contentNode.Remove();
                                    }

                                    // Injection headers
                                    var headerInjection = this.GetInjectionHeaders(asset, htmlContent.DescendantNodes().OfType<XElement>().Any(o => o.Name == xs_xhtml + "ui-view"));
                                    var headElement = (htmlContent.Element(xs_xhtml + "head") as XElement);
                                    headElement?.Add(headerInjection.Where(o => !headElement.Elements(o.Name).Any(e => (e.Attributes("src") != null && (e.Attributes("src") == o.Attributes("src"))) || (e.Attributes("href") != null && (e.Attributes("href") == o.Attributes("href"))))));


                                }
                            }
                            break;
                    } // switch


                    // Now process SSI directives - <!--#include virtual="XXXXXXX" -->
                    var includes = htmlContent.DescendantNodes().OfType<XComment>().Where(o => o?.Value?.Trim().StartsWith("#include virtual=\"") == true).ToList();
                    foreach (var inc in includes)
                    {
                        String assetName = inc.Value.Trim().Substring(18); // HACK: Should be a REGEX
                        if (assetName.EndsWith("\""))
                            assetName = assetName.Substring(0, assetName.Length - 1);
                        if (assetName == "content")
                            continue;
                        var includeAsset = this.ResolveAsset(assetName, asset);
                        if (includeAsset == null)
                        {
                            inc.AddAfterSelf(new XElement(xs_xhtml + "strong", new XText(String.Format("{0} NOT FOUND", assetName))));
                            inc.Remove();
                        }
                        else
                            using (MemoryStream ms = new MemoryStream(this.RenderAssetContent(includeAsset, preProcessLocalization, bindingParameters: bindingParameters)))
                            {
                                try
                                {
                                    var xel = XDocument.Load(ms).Elements().First() as XElement;
                                    if (xel.Name == xs_xhtml + "html")
                                        inc.AddAfterSelf(xel.Element(xs_xhtml + "body").Elements());
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
                        headTitle.Add(new XElement(xs_xhtml + "title", new XText(title)));
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
                        var assetString = this.GetStrings(preProcessLocalization);
                        retVal = this.m_localizationRegex.Replace(retVal, (m) => assetString.FirstOrDefault(o => o.Key == m.Groups[1].Value).Value ?? m.Groups[1].Value);
                    }

                    // Binding objects
                    if (bindingParameters != null)
                    {
                        retVal = this.m_bindingRegex.Replace(retVal, (m) => bindingParameters.TryGetValue(m.Groups[1].Value, out string v) ? v : m.ToString());
                    }
                    var byteData = Encoding.UTF8.GetBytes(retVal);
                    // Add to cache
                    lock (s_syncLock)
                        if (allowCache && !s_cache.ContainsKey(cacheKey))
                            s_cache.Add(cacheKey, byteData);

                    return byteData;
                }
            }
            else if (content is AppletAssetVirtual virtualContent) // Virtual asset 
            {
                if (!s_cache.TryGetValue(assetPath, out byte[] data))
                {
                    // TODO: Find a better way to do this
                    data = virtualContent.Include.SelectMany(includePath =>
                    {
                        var regExp = new Regex(includePath);
                        return asset.Manifest.Assets.Where(o => regExp.IsMatch(o.Name)).SelectMany(inclAsset => this.RenderAssetContent(inclAsset, preProcessLocalization, staticScriptRefs, allowCache, bindingParameters));
                    }).ToArray();

                    lock (s_syncLock)
                        if (allowCache && !s_cache.ContainsKey(cacheKey))
                            s_cache.Add(cacheKey, data);
                }
                return data;
            }
            else
                return null;
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
            if (htmlAsset == null && this.Resolver != null)
                htmlAsset = this.Resolver(asset) as AppletAssetHtml;

            // Insert scripts & Styles
            List<AssetScriptReference> scriptRefs = new List<AssetScriptReference>();
            if (htmlAsset == null)
                return scriptRefs;

            scriptRefs.AddRange(htmlAsset.Script.Where(o => o.IsStatic == false));

            // Content - SSI
            var includes = htmlAsset.Html.DescendantNodes().OfType<XComment>().Where(o => o?.Value?.Trim().StartsWith("#include virtual=\"") == true).ToList();
            foreach (var inc in includes)
            {
                String assetName = inc.Value.Trim().Substring(18); // HACK: Should be a REGEX
                if (assetName.EndsWith("\""))
                    assetName = assetName.Substring(0, assetName.Length - 1);
                if (assetName == "content")
                    continue;
                var includeAsset = this.ResolveAsset(assetName, asset);
                if (includeAsset != null)
                    scriptRefs.AddRange(this.GetLazyScripts(includeAsset));
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
        private List<XElement> GetInjectionHeaders(AppletAsset asset, bool isUiContainer)
        {
            var htmlAsset = asset.Content as AppletAssetHtml;
            if (htmlAsset == null && this.Resolver != null)
                htmlAsset = this.Resolver(asset) as AppletAssetHtml;

            // Insert scripts & Styles
            List<XElement> headerInjection = new List<XElement>();
            if (htmlAsset == null)
                return headerInjection;

            // Inject special headers
            foreach (var itm in htmlAsset.Bundle)
            {
                var bundle = this.m_referenceBundles.Find(o => o.Name == itm);
                if (bundle == null)
                    throw new FileNotFoundException(String.Format("Bundle {0} not found", itm));
                headerInjection.AddRange(bundle.Content.SelectMany(o => o.HeaderElement));
            }

            // All scripts
            if (isUiContainer) // IS A UI CONTAINER = ANGULAR UI REQUIRES ALL CONTROLLERS BE LOADED
                return this.ViewStateAssets.SelectMany(o => this.GetInjectionHeaders(o, false)).Distinct(new XElementEquityComparer()).ToList();
            else
                foreach (var itm in htmlAsset.Script.Where(o => o.IsStatic != false))
                {
                    var incAsset = this.ResolveAsset(itm.Reference, asset);
                    if (incAsset != null)
                        headerInjection.AddRange(new ScriptBundleContent(itm.Reference).HeaderElement);
                    else
                        throw new FileNotFoundException(String.Format("Asset {0} not found", itm.Reference));
                }
            foreach (var itm in htmlAsset.Style)
            {
                var incAsset = this.ResolveAsset(itm, asset);
                if (incAsset != null)
                    headerInjection.AddRange(new StyleBundleContent(itm).HeaderElement);

            }

            // Content - SSI
            var includes = htmlAsset.Html.DescendantNodes().OfType<XComment>().Where(o => o?.Value?.Trim().StartsWith("#include virtual=\"") == true).ToList();
            foreach (var inc in includes)
            {
                String assetName = inc.Value.Trim().Substring(18); // HACK: Should be a REGEX
                if (assetName.EndsWith("\""))
                    assetName = assetName.Substring(0, assetName.Length - 1);
                if (assetName == "content")
                    continue;
                var includeAsset = this.ResolveAsset(assetName, asset);
                if (includeAsset != null)
                    headerInjection.AddRange(this.GetInjectionHeaders(includeAsset, isUiContainer));
            }

            // Re-write
            foreach (var itm in headerInjection.OfType<XElement>().SelectMany(o => o.Attributes()).Where(o => o.Value.StartsWith("~")))
            {
                itm.Value = String.Format("/{0}/{1}", asset.Manifest.Info.Id, itm.Value.Substring(2));
                //itm.Value = itm.Value.Replace(APPLET_SCHEME, this.AppletBase).Replace(ASSET_SCHEME, this.AssetBase).Replace(DRAWABLE_SCHEME, this.DrawableBase);
            }
            return headerInjection.Distinct(new XElementEquityComparer()).ToList();
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
                    verified &= depItm != null && new Version(depItm?.Info.Version) >= new Version(itm.Version ?? "0.0.0.0");
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
        private class XElementEquityComparer : IEqualityComparer<XElement>
        {
            public bool Equals(XElement x, XElement y)
            {
                bool equals = true;
                foreach (var xa in x.Attributes())
                {
                    var ya = y.Attribute(xa.Name);
                    equals &= xa.Value == ya?.Value;
                }
                return equals;
            }


            /// <summary>
            /// Get Hash code
            /// </summary>
            public int GetHashCode(XElement obj)
            {
                return obj.Attribute("src")?.Value.GetHashCode() ?? obj.Attribute("href")?.Value.GetHashCode() ?? obj.GetHashCode();
            }
        }

        /// <summary>
        /// Get the template instance with the specified parameters
        /// </summary>
        /// <param name="templateId">The identifier of the template to fetch</param>
        /// <param name="parameters">The parameters to use</param>
        public IdentifiedData GetTemplateInstance(string templateId, IDictionary<String, String> parameters = null)
        {
            var definition = this.GetTemplateDefinition(templateId);
            if (definition == null)
                throw new FileNotFoundException($"Template {templateId} not found");

            var definitionAsset = this.ResolveAsset(definition.Definition);
            if (definitionAsset == null)
                throw new FileNotFoundException($"Template content {definition.Definition} not found");

            if (parameters == null)
                parameters = new Dictionary<String, String>();
            parameters.Add("today", DateTime.Now.Date.ToString("yyyy-MM-dd"));
            parameters.Add("now", DateTime.Now.ToString("o"));

            using (var ms = new MemoryStream(this.RenderAssetContent(definitionAsset, bindingParameters: parameters, allowCache: false)))
            using (var json = new JsonViewModelSerializer())
            {
                var result = json.DeSerialize<IdentifiedData>(ms);
                if (result is IHasTemplate template) // Correct any type-os in the JSON
                    template.Template = new TemplateDefinition() { Mnemonic = templateId };
                return result;
            }
        }

    }
}
