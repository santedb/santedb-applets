/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * Date: 2017-9-1
 */
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

#pragma warning disable CS1591
namespace SanteDB.Core.Applets.Model
{

    /// <summary>
    /// The subscription modes in which a filter definition applies
    /// </summary>
    [XmlType(nameof(ApplicationSubscriptionMode), Namespace = "http://santedb.org/applet")]
    public enum ApplicationSubscriptionMode
    {
        [XmlEnum("subscription")]
        Subscription = 1,
        [XmlEnum("all")]
        All = 2,
        [XmlEnum("*")]
        AllOrSubscription = 3
    }

    /// <summary>
    /// Represents synchronization pull triggers
    /// </summary>
    [XmlType(nameof(AppletSynchronizationTriggerType), Namespace = "http://santedb.org/applet")]
    [Flags]
    public enum AppletSynchronizationTriggerType
    {
        [XmlEnum("never")]
        Never = 0x0,
        [XmlEnum("always")]
        Always = OnStart | OnCommit | OnStop | OnPush | OnNetworkChange | PeriodicPoll,
        [XmlEnum("on-start")]
        OnStart = 0x01,
        [XmlEnum("on-commit")]
        OnCommit = 0x02,
        [XmlEnum("on-stop")]
        OnStop = 0x04,
        [XmlEnum("on-push")]
        OnPush = 0x08,
        [XmlEnum("on-x-net")]
        OnNetworkChange = 0x10,
        [XmlEnum("periodic")]
        PeriodicPoll = 0x20,
        [XmlEnum("manual")]
        Manual = 0x40
    }

    /// <summary>
    /// Applet subscription definition
    /// </summary>
    [XmlType(nameof(AppletSubscriptionDefinition), Namespace = "http://santedb.org/applet")]
    public class AppletSubscriptionDefinition
    {

        /// <summary>
        /// Gets or sets the resource involved in the subscription
        /// </summary>
        [XmlAttribute("resource"), JsonProperty("resource")]
        public String ResourceName { get; set; }

        /// <summary>
        /// Gets or sets the name involved in the subscription
        /// </summary>
        [XmlAttribute("name"), JsonProperty("name")]
        public String Name { get; set; }

        /// <summary>
        /// The mode(s) in which the subscription is valid
        /// </summary>
        [XmlAttribute("mode"), JsonProperty("mode")]
        public ApplicationSubscriptionMode Mode { get; set; }

        /// <summary>
        /// Ignore modified on
        /// </summary>
        [XmlAttribute("ignoreModifiedOn"), JsonProperty("ignoreModifiedOn")]
        public bool IgnoreModifiedOn { get; set; }

        /// <summary>
        /// The triggers on which the subscription is triggerd
        /// </summary>
        [XmlAttribute("trigger"), JsonProperty("trigger")]
        public AppletSynchronizationTriggerType Trigger { get; set; }

        /// <summary>
        /// Gets or sets the guard which prevents the filter from being applied
        /// </summary>
        [XmlAttribute("guard"), JsonProperty("guard")]
        public String Guard { get; set; }

        /// <summary>
        /// Gets or sets the filter which the resource uses
        /// </summary>
        [XmlElement("filter"), JsonProperty("filter")]
        public List<String> Filter { get; set; }
    }

}
#pragma warning restore CS1591
