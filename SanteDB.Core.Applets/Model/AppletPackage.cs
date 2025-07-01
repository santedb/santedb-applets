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
using Newtonsoft.Json;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Security;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Model
{
    /// <summary>
    /// Applet package used for installations only
    /// </summary>
    [XmlType(nameof(AppletPackage), Namespace = "http://santedb.org/applet")]
    [XmlRoot(nameof(AppletPackage), Namespace = "http://santedb.org/applet")]
    [ExcludeFromCodeCoverage]
    public class AppletPackage
    {


        /// <summary>
        /// Applet package
        /// </summary>
        public AppletPackage()
        {
            this.Version = typeof(AppletPackage).Assembly.GetName().Version.ToString();
        }

        // Serializer
        private static XmlSerializer s_packageSerializer = XmlModelSerializerFactory.Current.CreateSerializer(typeof(AppletPackage));
        private static XmlSerializer s_solutionSerializer = XmlModelSerializerFactory.Current.CreateSerializer(typeof(AppletSolution));
        private static Tracer m_tracer = Tracer.GetTracer(typeof(AppletPackage));

        // JF - Mobile Performance - Cache the unpacked applet
        private AppletManifest m_unpackedManifest;

        /// <summary>
        /// Load the specified manifest name
        /// </summary>
        public static AppletPackage Load(byte[] resourceData)
        {
            using (MemoryStream ms = new MemoryStream(resourceData))
            {
                return AppletPackage.Load(ms);
            }
        }

        /// <summary>
        /// Load the specified manifest name
        /// </summary>
        public static AppletPackage Load(Stream resourceStream)
        {
            using (GZipStream gzs = new GZipStream(resourceStream, CompressionMode.Decompress))
            {
                using (var xr = XmlReader.Create(gzs))
                {
                    AppletPackage retVal = null;
                    if (s_packageSerializer.CanDeserialize(xr))
                    {
                        retVal = s_packageSerializer.Deserialize(xr) as AppletPackage;
                    }
                    else if (s_solutionSerializer.CanDeserialize(xr))
                    {
                        retVal = s_solutionSerializer.Deserialize(xr) as AppletSolution;
                    }

                    return retVal;
                }
            }
        }

        /// <summary>
        /// Applet reference metadata
        /// </summary>
        [XmlElement("info"), JsonProperty("info")]
        public AppletInfo Meta
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or ses the manifest to be installed
        /// </summary>
        /// <value>The manifest.</value>
        [XmlElement("manifest"), JsonIgnore]
        public byte[] Manifest
        {
            get;
            set;
        }

        /// <summary>
        /// The pak version
        /// </summary>
        [XmlAttribute("pakVersion"), JsonIgnore]
        public String Version { get; set; }

        /// <summary>
        /// Public signing certificate
        /// </summary>
        [XmlElement("certificate"), JsonIgnore]
        public byte[] PublicKey { get; set; }

        /// <summary>
        /// Initial applet configuration
        /// </summary>
        /// <value>The configuration.</value>
        [XmlArray("settings"), XmlArrayItem("add")]
        public List<AppletSettingEntry> Settings
        {
            get;
            set;
        }

        /// <summary>
        /// Unpack the package
        /// </summary>
        public AppletManifest Unpack()
        {
            if (this.m_unpackedManifest == null && this.Manifest != null)
            {
                using (MemoryStream ms = new MemoryStream(this.Manifest))
                using (LZipStream gs = new LZipStream(NonDisposingStream.Create(ms), CompressionMode.Decompress))
                {
                    this.m_unpackedManifest =  AppletManifest.Load(gs);
                    if (this.PublicKey != null)
                    {
                        this.m_unpackedManifest.PublisherCertificate = new X509Certificate2(this.PublicKey);
                    }
                }
            }
            return this.m_unpackedManifest;

        }

        /// <summary>
        /// Save the compressed applet manifest
        /// </summary>
        public void Save(Stream stream)
        {
            using (GZipStream gzs = new GZipStream(NonDisposingStream.Create(stream), CompressionMode.Compress))
            {
                if (this is AppletSolution)
                {
                    s_solutionSerializer.Serialize(gzs, this);
                }
                else
                {
                    s_packageSerializer.Serialize(gzs, this);
                }
            }
        }

        /// <summary>
        /// Verify signatures of the applets
        /// </summary>
        public bool VerifySignatures(bool allowUnsignedApplets, IPlatformSecurityProvider platformSecurityProvider = null)
        {
            byte[] verifyBytes = this.Manifest;
            // First check: Hash - Make sure the HASH is ok
            if (this is AppletSolution asln)
            {
                verifyBytes = asln.Include.SelectMany(o => o.Manifest).ToArray();
                if (BitConverter.ToString(SHA256.Create().ComputeHash(verifyBytes)) != BitConverter.ToString(this.Meta.Hash))
                {
                    throw new InvalidOperationException($"Package contents of {this.Meta.Id} appear to be corrupt!");
                }
            }
            else if (BitConverter.ToString(SHA256.Create().ComputeHash(this.Manifest)) != BitConverter.ToString(this.Meta.Hash))
            {
                throw new InvalidOperationException($"Package contents of {this.Meta.Id} appear to be corrupt!");
            }

            if (this.Meta.Signature != null)
            {
                X509Certificate2 cert = null;

                if (null != platformSecurityProvider)
                {
                    m_tracer.TraceInfo("Will verify package {0} using platform security provider", this.Meta.Id.ToString());

                    if (null == this.PublicKey || this.PublicKey.Length == 0)
                    {
                        if (this.Meta.PublicKeyToken != null)
                        {
                            m_tracer.TraceInfo("Package does not have an embedded certificate and is signed which will be unsupported in the future.");
                            var certs = platformSecurityProvider.FindAllCertificates(X509FindType.FindByThumbprint, this.Meta.PublicKeyToken, StoreName.TrustedPublisher, StoreLocation.LocalMachine, validOnly: true);

                            if (certs?.Any() == true)
                            {
                                cert = certs.First();
                            }
                            else
                            {
                                throw new SecurityException($"Cannot find public key of publisher information for {this.Meta.PublicKeyToken} or the local certificate is invalid");
                            }
                        }
                    }
                    else
                    {
                        cert = new X509Certificate2(this.PublicKey);

                        if (!platformSecurityProvider.IsCertificateTrusted(cert, this.Meta.TimeStamp))
                        {
                            throw new SecurityException($"Package {this.Meta.Id} has certificate {cert.Thumbprint} which is not trusted by the platform.");
                        }
                    }
                }
                else
                {
                    m_tracer.TraceInfo("Will verify package {0} using legacy method.", this.Meta.Id.ToString());

                    // Get the public key
                    var x509Store = new X509Store(StoreName.TrustedPublisher, StoreLocation.LocalMachine);
                    try
                    {
                        x509Store.Open(OpenFlags.ReadOnly);
                        var certs = x509Store.Certificates.Find(X509FindType.FindByThumbprint, this.Meta.PublicKeyToken, false);

                        if (certs.Count == 0)
                        {
                            if (this.PublicKey != null)
                            {
                                // Embedded cert and trusted CA
                                cert = new X509Certificate2(this.PublicKey);
                                if (!cert.IsTrustedIntern(new X509Certificate2Collection(), this.Meta.TimeStamp, out IEnumerable<X509ChainStatus> chainStatus))
                                {
                                    throw new SecurityException($"Cannot verify identity of publisher: \r\n {cert.Subject} thb: {cert.Thumbprint} issued by {cert.Issuer} \r\n- {String.Join(",", chainStatus.Select(o => o.Status))}");
                                }
                            }
                            else
                            {
                                throw new SecurityException($"Cannot find public key of publisher information for {this.Meta.PublicKeyToken} or the local certificate is invalid");
                            }
                        }
                        else
                        {
                            cert = certs[0];
                        }
                    }
                    finally
                    {
                        x509Store.Close();
                    }
                }

                // Verify signature
                var rsa = cert.GetRSAPublicKey();

                var retVal = rsa.VerifyData(verifyBytes, this.Meta.Signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); //rsa.VerifyData(verifyBytes, CryptoConfig.MapNameToOID("SHA1"), this.Meta.Signature);

                if (retVal == false)
                {
                    retVal = rsa.VerifyData(verifyBytes, this.Meta.Signature, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);

                    if (retVal)
                    {
                        m_tracer.TraceInfo("Applet {0} is signed using SHA1 which will be unsupported in the future.", this.Meta.Id);
                    }
                }

                // Verify timestamp
                var timestamp = this.Unpack().Info.TimeStamp; //TODO: Upgrade package format to support DTO instead of DateTime.
                if (timestamp > DateTime.Now)
                {
                    throw new SecurityException($"Package {this.Meta.Id} was published in the future and will not be loaded.");
                }
                else if (cert.NotAfter < timestamp || cert.NotBefore > timestamp)
                {
                    throw new SecurityException($"Cannot find public key of publisher information for {this.Meta.PublicKeyToken} or the local certificate is invalid");
                }

                if (retVal == true)
                {
                    m_tracer.TraceEvent(EventLevel.Informational, "SUCCESSFULLY VALIDATED: {0} v.{1}\r\n" +
                        "\tKEY TOKEN: {2}\r\n" +
                        "\tSIGNED BY: {3}\r\n" +
                        "\tVALIDITY: {4:yyyy-MMM-dd} - {5:yyyy-MMM-dd}\r\n" +
                        "\tISSUER: {6}",
                        this.Meta.Id, this.Meta.Version, cert.Thumbprint, cert.Subject, cert.NotBefore, cert.NotAfter, cert.Issuer);
                }
                else
                {
                    m_tracer.TraceEvent(EventLevel.Critical, ">> SECURITY ALERT : {0} v.{1} <<\r\n" +
                        "\tPACKAGE VALIDATION FAILED\r\n" +
                        "\tKEY TOKEN (CLAIMED): {2}\r\n" +
                        "\tSIGNED BY  (CLAIMED): {3}\r\n" +
                        "\tVALIDITY: {4:yyyy-MMM-dd} - {5:yyyy-MMM-dd}\r\n" +
                        "\tISSUER: {6}\r\n\tSERVICE WILL HALT",
                        this.Meta.Id, this.Meta.Version, cert.Thumbprint, cert.Subject, cert.NotBefore, cert.NotAfter, cert.Issuer);
                }
                return retVal;


            }
            else if (allowUnsignedApplets)
            {
                m_tracer.TraceEvent(EventLevel.Warning, "Package {0} v.{1} (publisher: {2}) is not signed. To prevent unsigned applets from being installed disable the configuration option", this.Meta.Id, this.Meta.Version, this.Meta.Author);
                return true;
            }
            else
            {
                m_tracer.TraceEvent(EventLevel.Critical, "Package {0} v.{1} (publisher: {2}) is not signed and cannot be installed", this.Meta.Id, this.Meta.Version, this.Meta.Author);
                return false;
            }
        }
    }
}

