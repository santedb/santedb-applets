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
 * Date: 2024-6-12
 */
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.Templates;
using SanteDB.Core.Templates.Definition;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Core.Applets.Services.Impl
{
    /// <summary>
    /// Represents an applet template definition installer which 
    /// </summary>
    public class AppletTemplateDefinitionInstaller
    {
        private readonly ICarePathwayDefinitionRepositoryService m_carePathwayRepository;
        private readonly ITemplateDefinitionRepositoryService m_templateDefinitionRepository;
        private readonly IDataTemplateManagementService m_templateManagementService;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AppletTemplateDefinitionInstaller));

        /// <summary>
        /// Applet template definition service
        /// </summary>
        public AppletTemplateDefinitionInstaller(IAppletManagerService appletManagerService,
            ITemplateDefinitionRepositoryService templateDefinitionRepositoryService = null,
            IDataTemplateManagementService templateManagementService = null,
            ICarePathwayDefinitionRepositoryService carePathwayDefinitionRepositoryService = null,
            IAppletSolutionManagerService appletSolutionManagerService = null)
        {
            this.m_templateManagementService = templateManagementService;
            this.m_carePathwayRepository = carePathwayDefinitionRepositoryService;
            this.m_templateDefinitionRepository = templateDefinitionRepositoryService;
            appletManagerService.Changed += (o, e) =>
            {
            };
            if (appletSolutionManagerService != null)
            {
                appletSolutionManagerService.Solutions.ForEach(sln => this.InstallTemplatesFromApplet(appletSolutionManagerService.GetApplets(sln.Meta.Id)));
            }
            else
            {
                this.InstallTemplatesFromApplet(appletManagerService.Applets);
            }
        }

        /// <summary>
        /// Install the template definitions
        /// </summary>
        private void InstallTemplatesFromApplet(ReadonlyAppletCollection appletCollection)
        {
            if (this.m_templateDefinitionRepository == null)
            {
                return;
            }

            try
            {
                using (AuthenticationContext.EnterSystemContext())
                {
                    foreach (var tpl in appletCollection.DefinedTemplates)
                    {
                        var existing = this.m_templateDefinitionRepository.Find(o => o.Mnemonic == tpl.Mnemonic || o.Key == tpl.Uuid).FirstOrDefault();
                        if (existing == null ||
                            existing.Mnemonic != tpl.Mnemonic ||
                            existing.Name != tpl.Description ||
                            existing.Oid != tpl.Oid)
                        {

                            this.m_templateDefinitionRepository.Save(new Core.Model.DataTypes.TemplateDefinition()
                            {
                                Key = tpl.Uuid,
                                Mnemonic = tpl.Mnemonic,
                                Name = tpl.Description,
                                Oid = tpl.Oid,
                                Description = $"Definition found in {tpl.Definition}"
                            });
                        }

                        // Install the template definition and upgrade if newer version and/or priority is higher
                        var dataTemplateDefinition = this.CreateTemplateDefinition(tpl);
                        this.m_templateManagementService.AddOrUpdate(dataTemplateDefinition);
                    }
                    foreach (var cpd in appletCollection.DefinedPathways)
                    {
                        var existing = this.m_carePathwayRepository.Find(o => o.Mnemonic == cpd.Mnemonic || o.Key == cpd.Uuid).FirstOrDefault();
                        TemplateDefinition templateDefinition = null;
                        if (!String.IsNullOrEmpty(cpd.EncounterTemplate))
                        {
                            templateDefinition = this.m_templateDefinitionRepository.GetTemplateDefinition(cpd.EncounterTemplate);
                        }

                        if (existing == null ||
                            existing.Mnemonic != cpd.Mnemonic ||
                            existing.Name != cpd.Name ||
                            existing.Description != cpd.Description ||
                            existing.EnrollmentMode != cpd.EnrollmentMode ||
                            existing.TemplateKey != templateDefinition?.Key ||
                            existing.EligibilityCriteria != cpd.EligibilityCriteria)
                        {
                            this.m_carePathwayRepository.Save(new Core.Model.Acts.CarePathwayDefinition()
                            {
                                Key = cpd.Uuid,
                                Name = cpd.Name,
                                Mnemonic = cpd.Mnemonic,
                                Description = cpd.Description,
                                EnrollmentMode = cpd.EnrollmentMode,
                                EligibilityCriteria = cpd.EligibilityCriteria,
                                TemplateKey = templateDefinition?.Key
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceWarning("Could not install template definitions from applet - {0}", e);
            }
        }

        private DataTemplateDefinition CreateTemplateDefinition(AppletTemplateDefinition tpl)
        {
            var retVal = new DataTemplateDefinition()
            {
                Uuid = tpl.Uuid,
                Name = tpl.Description,
                Guard = tpl.Guard,
                Key = tpl.Uuid,
                Version = tpl.Priority + 1,
                Author = new List<string>() { tpl.Manifest?.Info?.Author },
                Icon = tpl.Icon,
                Mnemonic = tpl.Mnemonic,
                Oid = tpl.Oid,
                Public = tpl.Public,
                Readonly = true,
                Scopes = tpl.Scope,
                Views = new List<DataTemplateView>(),
                IsActive = true
            };

            // Render the contents
            if (!String.IsNullOrEmpty(tpl.Definition))
            {
                retVal.JsonTemplate = new DataTemplateContent()
                {
                    Content = tpl.Definition,
                    ContentType = DataTemplateContentType.reference
                };
            }

            if (!String.IsNullOrEmpty(tpl.View))
            {
                retVal.Views.Add(new DataTemplateView()
                {
                    ViewType = DataTemplateViewType.DetailView,
                    Content = tpl.View
                });
            }
            if (!String.IsNullOrEmpty(tpl.Summary))
            {
                retVal.Views.Add(new DataTemplateView()
                {
                    ViewType = DataTemplateViewType.SummaryView,
                    Content = tpl.Summary
                });
            }
            if (!String.IsNullOrEmpty(tpl.Form))
            {
                retVal.Views.Add(new DataTemplateView()
                {
                    ViewType = DataTemplateViewType.Entry,
                    Content = tpl.Form
                });
            }

            if (!String.IsNullOrEmpty(tpl.BackEntry))
            {
                retVal.Views.Add(new DataTemplateView()
                {
                    ViewType = DataTemplateViewType.BackEntry,
                    Content = tpl.BackEntry
                });
            }
            return retVal;


        }
    }
}
