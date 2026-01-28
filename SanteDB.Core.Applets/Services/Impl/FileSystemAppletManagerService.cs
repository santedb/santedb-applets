/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using SanteDB.Core.Applets.Configuration;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SanteDB.Core.Applets.Services.Impl
{
    /// <summary>
    /// Represents an applet manager service that uses the local file system
    /// </summary>
    [ServiceProvider("Local Applet Repository/Manager", Configuration = typeof(AppletConfigurationSection))]
    public class FileSystemAppletManagerService : IAppletManagerService, IAppletSolutionManagerService, IReportProgressChanged
    {
        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Local Applet Repository/Manager";

        // Lock object
        private object m_lockObject = new object();

        // Solutions registered
        private ObservableCollection<AppletSolution> m_solutions = new ObservableCollection<AppletSolution>();

        /// <summary>
        /// The applet collection 
        /// </summary>
        protected Dictionary<String, AppletCollection> m_appletCollection = new Dictionary<string, AppletCollection>();

        // Applet collection for readonly applets
        private Dictionary<String, ReadonlyAppletCollection> m_readonlyAppletCollection = new Dictionary<string, ReadonlyAppletCollection>();

        // Map of package id to file
        private Dictionary<String, String> m_fileDictionary = new Dictionary<string, string>();

        /// <summary>
        /// The configuration injected into the service
        /// </summary>
        protected readonly AppletConfigurationSection m_configuration;

        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(FileSystemAppletManagerService));

        readonly IPlatformSecurityProvider _PlatformSecurityProvider;

        /// <summary>
        /// Local applet manager ctor
        /// </summary>
        public FileSystemAppletManagerService(IConfigurationManager configurationManager, IPlatformSecurityProvider platformSecurityProvider)
        {
            _PlatformSecurityProvider = platformSecurityProvider;

            var defaultApplet = new AppletCollection();
            this.m_appletCollection.Add(String.Empty, defaultApplet); // Default applet
            this.m_configuration = configurationManager.GetSection<AppletConfigurationSection>();

            // Load the applets
            this.LoadApplets();
        }

        /// <summary>
        /// Gets the loaded applets from the manager
        /// </summary>
        public ReadonlyAppletCollection Applets
        {
            get
            {
                return this.GetApplets(String.Empty);
            }
        }

        /// <summary>
        /// Get the solutions
        /// </summary>
        public IEnumerable<AppletSolution> Solutions => this.m_solutions;

        /// <summary>
        /// Applet has changed
        /// </summary>
        public event EventHandler Changed;

        /// <inheritdoc/>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        /// <summary>
        /// Get the specified applet
        /// </summary>
        public virtual byte[] GetPackage(String appletId)
        {
            return this.GetPackage(String.Empty, appletId);
        }

        /// <summary>
        /// Get the specified package data
        /// </summary>
        public virtual byte[] GetPackage(String solutionId, String appletId)
        {
            this.m_tracer.TraceInfo("Retrieving package {0}", appletId);

            // Save the applet
            var appletDir = Path.Combine(this.m_configuration.AppletDirectory, solutionId);
            if (!Directory.Exists(appletDir))
            {
                Directory.CreateDirectory(appletDir);
            }

            // Install
            String pakFile = null;
            if (this.m_fileDictionary.TryGetValue($"{solutionId}{appletId}", out pakFile) && File.Exists(pakFile))
            {
                return File.ReadAllBytes(pakFile);
            }
            else
            {
                throw new FileNotFoundException($"Applet {appletId} not found");
            }
        }

        /// <summary>
        /// Uninstall the applet package
        /// </summary>
        public virtual bool UnInstall(String packageId)
        {
            this.m_tracer.TraceInfo("Un-installing {0}", packageId);
            // Applet check
            var applet = this.m_appletCollection[String.Empty].FirstOrDefault(o => o.Info.Id == packageId);
            if (applet == null) // Might be solution
            {
                var soln = this.m_solutions.FirstOrDefault(o => o.Meta.Id == packageId);
                if (soln == null)
                {
                    throw new FileNotFoundException($"Applet {packageId} is not installed");
                }
                else
                {
                    this.m_solutions.Remove(soln);
                    lock (this.m_fileDictionary)
                    {
                        if (this.m_fileDictionary.ContainsKey(packageId + ".sln"))
                        {
                            File.Delete(this.m_fileDictionary[packageId + ".sln"]);
                        }
                    }
                }
            }
            else
            {
                // Dependency check
                var dependencies = this.m_appletCollection[String.Empty].Where(o => o.Info.Dependencies.Any(d => d.Id == packageId));
                if (dependencies.Any())
                {
                    throw new InvalidOperationException($"Uninstalling {packageId} would break : {String.Join(", ", dependencies.Select(o => o.Info))}");
                }

                // We're good to go!
                this.m_appletCollection[String.Empty].Remove(applet);

                lock (this.m_fileDictionary)
                {
                    if (this.m_fileDictionary.ContainsKey(packageId))
                    {
                        File.Delete(this.m_fileDictionary[packageId]);
                    }
                }

                this.m_appletCollection[String.Empty].ClearCaches();
            }
            return true;
        }

        /// <summary>
        /// Install package
        /// </summary>
        public virtual bool Install(AppletPackage package, bool isUpgrade = false)
        {
            return this.Install(package, isUpgrade, null);
        }

        /// <summary>
        /// Performs an installation
        /// </summary>
        public virtual bool Install(AppletPackage package, bool isUpgrade, AppletSolution owner)
        {
            this.m_tracer.TraceInfo("Installing {0}", package.Meta);

            var appletScope = owner?.Meta.Id ?? String.Empty;

            // Save the applet
            var appletDir = this.m_configuration.AppletDirectory;
            if (!Path.IsPathRooted(appletDir))
            {
                appletDir = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), this.m_configuration.AppletDirectory);
            }

            if (owner != null)
            {
                appletDir = Path.Combine(appletDir, owner.Meta.Id);
            }

            if (!Directory.Exists(appletDir))
            {
                Directory.CreateDirectory(appletDir);
            }

            // Install
            var pakFile = Path.Combine(appletDir, package.Meta.Id + ".pak");
            if (this.m_appletCollection[appletScope].Any(o => o.Info.Id == package.Meta.Id) && File.Exists(pakFile) && !isUpgrade)
            {
                throw new InvalidOperationException($"Cannot replace {package.Meta} unless upgrade is specifically specified");
            }

            using (var fs = File.Create(pakFile))
            {
                package.Save(fs);
            }

            lock (this.m_fileDictionary)
            {
                if (!this.m_fileDictionary.ContainsKey($"{appletScope}{package.Meta.Id}"))
                {
                    this.m_fileDictionary.Add($"{appletScope}{package.Meta.Id}", pakFile);
                }
            }

            return this.LoadPackage(package, appletScope);
        }


        /// <summary>
        /// Get applet
        /// </summary>
        public virtual AppletManifest GetApplet(string appletId)
        {

            return this.GetApplet(String.Empty, appletId);
        }

        /// <summary>
        /// Gets the specified applet manifest
        /// </summary>
        public virtual AppletManifest GetApplet(string solutionId, string appletId)
        {
            return this.GetApplets(solutionId).FirstOrDefault(o => o.Info.Id == appletId);
        }

        /// <summary>
        /// Gets the specified applet manifest
        /// </summary>
        public virtual ReadonlyAppletCollection GetApplets(string solutionId)
        {
            if (!this.m_readonlyAppletCollection.TryGetValue(solutionId ?? string.Empty, out var retVal))
            {
                lock (this.m_lockObject)
                {
                    if (!this.m_readonlyAppletCollection.TryGetValue(solutionId, out retVal))
                    {
                        retVal = this.m_appletCollection[solutionId].AsReadonly();
                        this.m_readonlyAppletCollection.Add(solutionId, retVal);
                    }
                }
            }
            return retVal;
        }

        /// <summary>
        /// Load an applet
        /// </summary>
        public virtual bool LoadApplet(AppletManifest applet)
        {
            throw new SecurityException("Cannot directly load applets on the server - call Install instead");

        }

        /// <summary>
        /// Install the specified applet solution
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="isUpgrade"></param>
        /// <returns></returns>
        public virtual bool Install(AppletSolution solution, bool isUpgrade = false)
        {
            this.m_tracer.TraceInfo("Installing solution {0}", solution.Meta);

            if (this.m_appletCollection.TryGetValue(solution.Meta.Id, out var existingSolutionCollection))
            {
                this.m_tracer.TraceInfo("Upgrading solution {0}", solution.Meta.Id);
                existingSolutionCollection.Clear();
                //this.m_appletCollection.Remove(solution.Meta.Id);
            }
            else
            {
                this.m_appletCollection.Add(solution.Meta.Id, new AppletCollection());
            }

            // Save the applet
            var appletDir = this.m_configuration.AppletDirectory;
            if (!Path.IsPathRooted(appletDir))
            {
                appletDir = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), this.m_configuration.AppletDirectory);
            }

            if (!Directory.Exists(appletDir))
            {
                Directory.CreateDirectory(appletDir);
            }

            // Install
            var pakFile = Path.Combine(appletDir, solution.Meta.Id + ".pak");
            var existingSolution = this.m_solutions.FirstOrDefault(o => o.Meta.Id == solution.Meta.Id);
            if (existingSolution != null && File.Exists(pakFile))
            {
                if (!isUpgrade)
                {
                    throw new InvalidOperationException($"Cannot replace {solution.Meta} unless upgrade is specifically specified");
                }
                this.m_solutions.Remove(existingSolution);
            }

            using (var fs = File.Create(pakFile))
            {
                solution.Save(fs);
            }

            // Unpack items from the solution package and install if needed
            foreach (var itm in solution.Include.Where(o => o.Manifest != null))
            {
                var installedApplet = this.GetApplet(solution.Meta.Id, itm.Meta.Id);
                if (installedApplet == null ||
                    installedApplet.Info.Version.ParseVersion(out _) < itm.Meta.Version.ParseVersion(out _)) // TODO: Allow for equal versions if the suffix is newer 
                                                                                                             // Installed version is there but is older or is not installed, so we install it
                {
                    this.m_tracer.TraceInfo("Installing Solution applet {0} v{1}...", itm.Meta.Id, itm.Meta.Version);
                    this.Install(itm, true, solution);
                }
            }

            // Register the pakfile
            lock (this.m_fileDictionary)
            {
                string solutionkey = solution.Meta.Id + ".sln";

                if (!this.m_fileDictionary.ContainsKey(solutionkey))
                {
                    this.m_fileDictionary.Add(solutionkey, pakFile);
                }
            }

            this.m_solutions.Add(solution);

            foreach (var apl in this.m_appletCollection[solution.Meta.Id])
            {
                var existing = this.m_appletCollection[String.Empty].FirstOrDefault(o => o.Info.Id == apl.Info.Id);
                if (existing == null || existing.Info.Version.ParseVersion(out _) < apl.Info.Version.ParseVersion(out _))
                {
                    this.m_appletCollection[String.Empty].Remove(apl);
                    this.m_appletCollection[String.Empty].Add(apl);
                }
            }
            this.Changed?.Invoke(this, EventArgs.Empty);

            return true;
        }

        /// <inheritdoc/>
        public bool LoadApplets()
        {
            try
            {
                // Load packages from applets/ filesystem directory
                var appletDir = this.m_configuration.AppletDirectory;
                if (!Path.IsPathRooted(appletDir))
                {
                    var location = Assembly.GetEntryAssembly()?.Location ?? Assembly.GetExecutingAssembly().Location;

                    appletDir = Path.Combine(Path.GetDirectoryName(location), this.m_configuration.AppletDirectory);
                }

                if (!Directory.Exists(appletDir))
                {
                    this.m_tracer.TraceWarning("Applet directory {0} doesn't exist, no applets will be loaded", appletDir);
                }
                else
                {
                    this.m_tracer.TraceEvent(EventLevel.Verbose, "Scanning {0} for applets...", appletDir);
                    // JF - Provide feedback to caller in case of slow loading
                    var appletFiles = Directory.GetFiles(appletDir).OrderBy(o => o.EndsWith(".sln.pak") ? 0 : 1).ToArray();
                    int loadedApplets = 0;
                    foreach (var f in appletFiles)
                    {
                        // Try to open the file
                        this.m_tracer.TraceInfo("Loading {0}...", f);
                        this.ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(nameof(FileSystemAppletManagerService), (float)loadedApplets++ / (float)appletFiles.Length, $"Loading {Path.GetFileName(f)}"));
                        using (var fs = File.OpenRead(f))
                        {
                            var pkg = AppletPackage.Load(fs);

                            // TODO: Verify package hash / signature
                            if (!pkg.VerifySignatures(this.m_configuration.AllowUnsignedApplets, _PlatformSecurityProvider))
                            {
                                throw new SecurityException($"{pkg.GetType().Name} {pkg.Meta.Id} failed validation");
                            }


                            if (pkg is AppletSolution) // We have loaded a solution
                            {
                                if (this.m_solutions.Any(o => o.Meta.Id == pkg.Meta.Id))
                                {
                                    this.m_tracer.TraceEvent(EventLevel.Critical, "Duplicate solution {0} is not permitted", pkg.Meta.Id);
                                    throw new DuplicateNameException(pkg.Meta.Id);
                                }
                                else if (!this.Install(pkg as AppletSolution, true) && ApplicationServiceContext.Current.HostType != SanteDBHostType.Configuration)
                                {
                                    throw new InvalidOperationException($"Could not install applet solution {pkg.Meta.Id}");
                                }
                            }
                            else if (this.m_fileDictionary.ContainsKey(pkg.Meta.Id))
                            {
                                this.m_tracer.TraceEvent(EventLevel.Warning, "Skipping duplicate package {0}", pkg.Meta.Id);
                                continue;
                            }
                            else if (!this.LoadPackage(pkg, String.Empty))
                            {
                                this.m_tracer.TraceEvent(EventLevel.Critical, "Cannot proceed while untrusted applets are present");
                                throw new SecurityException("Cannot proceed while untrusted applets are present");
                            }
                        }
                    }
                }
            }
            catch (SecurityException e)
            {
                this.m_tracer.TraceEvent(EventLevel.Error, "Error loading applets: {0}", e);
                throw new InvalidOperationException("Cannot proceed while untrusted applets are present - Run `santedb --install-certs` or install the publisher certificate into `TrustedPublishers` certificate store", e);
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceEvent(EventLevel.Error, "Error loading applets: {0}", ex);
                throw;
            }
            return true;
        }

        private bool LoadPackage(AppletPackage package, String packageScope)
        {

            if (!this.m_appletCollection[packageScope].VerifyDependencies(package.Meta))
            {
                this.m_tracer.TraceWarning($"Applet {package.Meta} depends on : [{String.Join(", ", package.Meta.Dependencies.Select(o => o.ToString()))}] which are missing or incompatible");
            }

            var manifest = package.Unpack();
            // remove the package from the collection if this is an upgrade
            this.m_appletCollection[packageScope].Remove(manifest);
            this.m_appletCollection[packageScope].Add(manifest);
            this.m_appletCollection[packageScope].ClearCaches();

            if (String.IsNullOrEmpty(packageScope))
            {
                this.Changed?.Invoke(this, EventArgs.Empty);
            }
            return true;

        }
    }
}