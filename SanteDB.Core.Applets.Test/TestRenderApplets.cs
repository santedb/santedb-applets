/*
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SanteDB.Core.Applets.Model;
using System.Diagnostics;
using System.Text;
using System.IO;
using SanteDB.Core.Model.EntityLoader;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using System.Collections.Generic;
using System.Linq.Expressions;
using SanteDB.Core.Model.DataTypes;
using System.Linq;
using SanteDB.Core.Model.Query;

namespace SanteDB.Core.Applets.Test
{
    [TestClass]
    public class TestRenderApplets
    {
        // Applet collection
        private AppletCollection m_appletCollection = new AppletCollection();

        /// <summary>
        /// Initialize the test
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            this.m_appletCollection.Add(AppletManifest.Load(typeof(TestRenderApplets).Assembly.GetManifestResourceStream("SanteDB.Core.Applets.Test.HelloWorldApplet.xml")));
            this.m_appletCollection.Add(AppletManifest.Load(typeof(TestRenderApplets).Assembly.GetManifestResourceStream("SanteDB.Core.Applets.Test.SettingsApplet.xml")));
            this.m_appletCollection.Add(AppletManifest.Load(typeof(TestRenderApplets).Assembly.GetManifestResourceStream("SanteDB.Core.Applets.Test.LocalizationWithJavascript.xml")));
            this.m_appletCollection.Add(AppletManifest.Load(typeof(TestRenderApplets).Assembly.GetManifestResourceStream("SanteDB.Core.Applets.Test.LayoutAngularTest.xml")));
        }

        /// <summary>
        /// Entity source provider test
        /// </summary>
        private class TestEntitySource : IEntitySourceProvider
        {
            public TObject Get<TObject>(Guid? key) where TObject : IdentifiedData, new()
            {
                return new TObject() { Key = key };
            }

            public TObject Get<TObject>(Guid? key, Guid? versionKey) where TObject : IdentifiedData, new()
            {
                return new TObject() { Key = key };
            }

            public IQueryResultSet<TObject> GetRelations<TObject>(Guid? sourceKey, int? sourceVersionSequence) where TObject : IdentifiedData, IVersionedAssociation, new()
            {
                throw new NotImplementedException();
            }

            public IQueryResultSet<TObject> GetRelations<TObject>(params Guid?[] sourceKey) where TObject : IdentifiedData, ISimpleAssociation, new()
            {
                return new MemoryQueryResultSet<TObject>(new TObject[0]);
            }


            public IQueryResultSet GetRelations(Type tmodel, params Guid?[] sourceKey) 
            {
                return new MemoryQueryResultSet(new Object[0]);
            }

            /// <summary>
            /// Query the specified object
            /// </summary>
            public IQueryResultSet<TObject> Query<TObject>(Expression<Func<TObject, bool>> query) where TObject : IdentifiedData, new()
            {
                if (typeof(TObject) == typeof(Concept))
                {
                    // Add list of concepts
                    return new MemoryQueryResultSet<TObject>(new List<Concept>()
                    {
                        new Concept()
                        {
                            Key = Guid.NewGuid(),
                            Mnemonic = "Male",
                            ConceptNames = new List<ConceptName>()
                            {
                                new ConceptName() { Language = "en" ,Name = "Male" },
                                new ConceptName() { Language = "sw" , Name = "Kiume" }
                            }
                        },
                        new Concept()
                        {
                            Key = Guid.NewGuid(),
                            Mnemonic = "Female",
                            ConceptNames = new List<ConceptName>()
                            {
                                new ConceptName() { Language = "en" ,Name = "Female" },
                                new ConceptName() { Language = "sw" , Name = "Kike" }
                            }
                        },
                    }.OfType<TObject>());
                }

                if (typeof(TObject) == typeof(AssigningAuthority))
                {
                    return new MemoryQueryResultSet<TObject>(new List<AssigningAuthority>
                    {
                        new AssigningAuthority
                        {
                            Key = Guid.NewGuid(),
                            IsUnique = false,
                            DomainName = "Testing Identifier",
                            Name = "Testing Identifier",
                            ValidationRegex = "^[0-9]{10}$"
                        }
                    }.OfType<TObject>());
                }

                Assert.Fail();
                return null;
            }
        }

        [TestMethod]
        public void TestCreatePackage()
        {
            var package = this.m_appletCollection[1].CreatePackage();
            Assert.IsNotNull(package);
        }

        [TestMethod]
        public void TestResolveAbsolute()
        {
            Assert.IsNotNull(this.m_appletCollection.ResolveAsset("app://org.santedb.sample.helloworld/layout"));
        }

        [TestMethod]
        public void TestResolveIndex()
        {
            var asset = this.m_appletCollection.ResolveAsset("app://org.santedb.sample.helloworld/");
            Assert.IsNotNull(asset);
            Assert.AreEqual("index.html", asset.Name);
            Assert.AreEqual("en", asset.Language);
        }

        [TestMethod]
        public void TestResolveRelative()
        {
            var asset = this.m_appletCollection.ResolveAsset("app://org.santedb.sample.helloworld/index.html");
            Assert.IsNotNull(asset);
            Assert.IsNotNull(this.m_appletCollection.ResolveAsset("layout", relativeAsset: asset));
        }

        [TestMethod]
        public void TestResolveSettingLanguage()
        {
            var asset = this.m_appletCollection.ResolveAsset("app://org.santedb.applets.core.settings/");
            Assert.IsNotNull(asset);
        }

        [TestMethod]
        public void TestRenderSettingsHtml()
        {
            var asset = this.m_appletCollection.ResolveAsset("app://org.santedb.applets.core.settings/");
            var render = this.m_appletCollection.RenderAssetContent(asset);
            Trace.WriteLine(Encoding.UTF8.GetString(render));
        }

        [TestMethod]
        public void TestRenderHtml()
        {
            var asset = this.m_appletCollection.ResolveAsset("app://org.santedb.sample.helloworld/index.html");
            var render = this.m_appletCollection.RenderAssetContent(asset);
            Trace.WriteLine(Encoding.UTF8.GetString(render));
        }

        /// <summary>
        /// Test pre-processing of localization
        /// </summary>
        [TestMethod]
        public void TestPreProcessLocalization()
        {
            var asset = this.m_appletCollection.ResolveAsset("app://org.santedb.applet.test.layout/index.html");
            var render = this.m_appletCollection.RenderAssetContent(asset, "en");

            string html = Encoding.UTF8.GetString(render);
            Assert.IsFalse(html.Contains("{{ 'some_string' | i18n }}"));
            Assert.IsFalse(html.Contains("{{ ::'some_string' | i18n }}"));

            Assert.IsTrue(html.Contains("SOME STRING!"));
        }

        /// <summary>
        /// Test rendering
        /// </summary>
        [TestMethod]
        public void TestLayoutBundleReferences()
        {
            var coll = new AppletCollection();
            coll.Add(AppletManifest.Load(typeof(TestRenderApplets).Assembly.GetManifestResourceStream("SanteDB.Core.Applets.Test.LayoutAngularTest.xml")));

            var asset = coll.ResolveAsset("app://org.santedb.applet.test.layout/index.html");
            var render = coll.RenderAssetContent(asset);
            string html = Encoding.UTF8.GetString(render);
            Assert.IsTrue(html.Contains("index-controller"), "Missing index-controller");
            Assert.IsTrue(html.Contains("layout-controller"), "Missing layout-controller");
            Assert.IsTrue(html.Contains("index-style"), "Missing index-style");
            Assert.IsTrue(html.Contains("layout-controller"), "Missing layout-style");
        }
    }
}