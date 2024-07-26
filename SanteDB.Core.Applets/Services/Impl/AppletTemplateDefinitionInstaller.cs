using SanteDB.Core.Applets.Model;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Core.Applets.Services.Impl
{
    /// <summary>
    /// Represents an applet template definition installer which 
    /// </summary>
    public class AppletTemplateDefinitionInstaller
    {
        private readonly ITemplateDefinitionRepositoryService m_templateDefinitionRepository;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AppletTemplateDefinitionInstaller));

        /// <summary>
        /// Applet template definition service
        /// </summary>
        public AppletTemplateDefinitionInstaller(IAppletManagerService appletManagerService, ITemplateDefinitionRepositoryService templateDefinitionRepositoryService = null, IAppletSolutionManagerService appletSolutionManagerService = null)
        {

            this.m_templateDefinitionRepository = templateDefinitionRepositoryService;
            appletManagerService.Changed += (o,e) =>
            {
            };
            if(appletSolutionManagerService != null)
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
            if(this.m_templateDefinitionRepository == null)
            {
                return;
            }

            try
            {
                using (AuthenticationContext.EnterSystemContext()) {
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
                    }
                }
            }
            catch(Exception e)
            {
                this.m_tracer.TraceWarning("Could not install template definitions from applet - {0}", e);
            }
        }
    }
}
