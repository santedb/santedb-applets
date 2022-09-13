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
 * Date: 2022-5-30
 */
using System;
using SanteDB.Core.Applets.Model;
using System.Diagnostics;
using System.Text;
using System.IO;
using SanteDB.Core.Model.EntityLoader;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using SanteDB.Core.Model.DataTypes;
using System.Linq;
using SanteDB.Core.Model.Query;
using NUnit.Framework;

namespace SanteDB.Core.Applets.Test
{
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class TestRenderApplets
    {
        // Applet collection
        private AppletCollection m_appletCollection = new AppletCollection();

        /// <summary>
        /// Initialize the test
        /// </summary>
        [OneTimeSetUp]
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

                if (typeof(TObject) == typeof(IdentityDomain))
                {
                    return new MemoryQueryResultSet<TObject>(new List<IdentityDomain>
                    {
                        new IdentityDomain
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

        [Test]
        public void TestCreatePackage()
        {
            var package = this.m_appletCollection[1].CreatePackage();
            Assert.IsNotNull(package);
        }

        [Test]
        public void TestResolveAbsolute()
        {
            Assert.IsNotNull(this.m_appletCollection.ResolveAsset("/org.santedb.sample.helloworld/layout"));
        }

        [Test]
        public void TestResolveIndex()
        {
            var asset = this.m_appletCollection.ResolveAsset("/org.santedb.sample.helloworld/");
            Assert.IsNotNull(asset);
            Assert.AreEqual("index.html", asset.Name);
            Assert.AreEqual("en", asset.Language);
        }

        [Test]
        public void TestResolveRelative()
        {
            var asset = this.m_appletCollection.ResolveAsset("/org.santedb.sample.helloworld/index.html");
            Assert.IsNotNull(asset);
            Assert.IsNotNull(this.m_appletCollection.ResolveAsset("layout", relativeAsset: asset));
        }

        [Test]
        public void TestResolveSettingLanguage()
        {
            var asset = this.m_appletCollection.ResolveAsset("/org.santedb.applets.core.settings/");
            Assert.IsNotNull(asset);
        }

        [Test]
        public void TestRenderSettingsHtml()
        {
            var asset = this.m_appletCollection.ResolveAsset("/org.santedb.applets.core.settings/");
            var render = this.m_appletCollection.RenderAssetContent(asset);
            Trace.WriteLine(Encoding.UTF8.GetString(render));
        }

        [Test]
        public void TestRenderHtml()
        {
            var asset = this.m_appletCollection.ResolveAsset("/org.santedb.sample.helloworld/index.html");
            var render = this.m_appletCollection.RenderAssetContent(asset);
            Trace.WriteLine(Encoding.UTF8.GetString(render));
        }

        /// <summary>
        /// Test pre-processing of localization
        /// </summary>
        [Test]
        public void TestPreProcessLocalization()
        {
            var asset = this.m_appletCollection.ResolveAsset("/org.santedb.applet.test.layout/index.html");
            var render = this.m_appletCollection.RenderAssetContent(asset, "en");

            string html = Encoding.UTF8.GetString(render);
            Assert.IsFalse(html.Contains("{{ 'some_string' | i18n }}"));
            Assert.IsFalse(html.Contains("{{ ::'some_string' | i18n }}"));

            Assert.IsTrue(html.Contains("SOME STRING!"));
        }

        /// <summary>
        /// Test rendering
        /// </summary>
        [Test]
        public void TestLayoutBundleReferences()
        {
            var coll = new AppletCollection();
            coll.Add(AppletManifest.Load(typeof(TestRenderApplets).Assembly.GetManifestResourceStream("SanteDB.Core.Applets.Test.LayoutAngularTest.xml")));

            var asset = coll.ResolveAsset("/org.santedb.applet.test.layout/index.html");
            var render = coll.RenderAssetContent(asset);
            string html = Encoding.UTF8.GetString(render);
//            Assert.IsTrue(html.Contains("index-controller"), "Missing index-controller");
            
            Assert.IsTrue(html.Contains("layout-controller"), "Missing layout-controller");
            Assert.IsTrue(html.Contains("index-style"), "Missing index-style");
            Assert.IsTrue(html.Contains("layout-controller"), "Missing layout-style");
        }

        /// <summary>
        /// Test we cannot add stuff to a readonly applet collection
        /// </summary>
        [Test]
        public void TestCannotAddToReadonly()
        {
            var coll = new AppletCollection();
            var am = AppletManifest.Load(
                typeof(TestRenderApplets).Assembly.GetManifestResourceStream(
                    "SanteDB.Core.Applets.Test.LayoutAngularTest.xml"));
            coll.Add(am);
            
            // cannot add to readonly 
            var ro = coll.AsReadonly();
            Assert.Throws<InvalidOperationException>(() => ro.Add(am));
            Assert.Throws<InvalidOperationException>(() => ro.Remove((am)));
            Assert.Throws<InvalidOperationException>(() => ro.Insert(0, am));
            Assert.Throws<InvalidOperationException>(() => ro.RemoveAt(0));
            Assert.Throws<InvalidOperationException>(() => ro.Clear());

            coll.Remove(am);
            coll.Insert(0, am);
            coll.RemoveAt(0);
coll.Add(am);
            var asf = coll.ResolveAsset("/org.santedb.applet.test.layout/index.html");
            Assert.IsNotNull(asf);
            Assert.AreEqual(1, coll.GetLazyScripts(asf).Count);
        }
    }
}