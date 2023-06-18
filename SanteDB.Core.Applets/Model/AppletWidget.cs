/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-5-19
 */
using Newtonsoft.Json;
using SanteDB.Core.Model.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace SanteDB.Core.Applets.Model
{


    /// <summary>
    /// Identifies the type which the widget is
    /// </summary>
    [XmlType(nameof(AppletWidgetType), Namespace = "http://santedb.org/applet")]
    public enum AppletWidgetType
    {
        /// <summary>
        /// The extension is a panel which is added to a panel container
        /// </summary>
        [XmlEnum("panel")]
        Panel,
        /// <summary>
        /// The extension is a tab
        /// </summary>
        [XmlEnum("tab")]
        Tab
    }

    /// <summary>
    /// Identifies the size of the widget
    /// </summary>
    [XmlType(nameof(AppletWidgetSize), Namespace = "http://santedb.org/applet")]
    public enum AppletWidgetSize
    {
        /// <summary>
        /// The applet widget should spen the entire width of screen
        /// </summary>
        [XmlEnum("l")]
        Large,
        /// <summary>
        /// The applet widget should be medium sized
        /// </summary>
        /// <remarks>
        /// On desktop this is 50% width, for tablets 50-100% width, and phones 100% width
        /// </remarks>
        [XmlEnum("m")]
        Medium,
        /// <summary>
        /// The applet widget should be the smallest horizontal widget
        /// </summary>
        /// <remarks>
        /// On a desktop, this is 25% width, for tablets 50% width and phones 100% width
        /// </remarks>
        [XmlEnum("s")]
        Small
    }

    /// <summary>
    /// Identifies the size of the widget
    /// </summary>
    [XmlType(nameof(AppletWidgetViewType), Namespace = "http://santedb.org/applet")]
    public enum AppletWidgetViewType : short
    {
        /// <summary>
        /// There is no special widget view
        /// </summary>
        [XmlEnum("none")]
        None = 0x0,
        /// <summary>
        /// Widget button is to show an alternate view
        /// </summary>
        [XmlEnum("alternate")]
        Alternate = 0x1,
        /// <summary>
        /// Widget button is to edit the widget
        /// </summary>
        [XmlEnum("edit")]
        Edit = 0x2,
        /// <summary>
        /// Widget button is to reload the widget
        /// </summary>
        [XmlEnum("setting")]
        Settings = 0x4
    }

    /// <summary>
    /// Widget view type
    /// </summary>
    [XmlType(nameof(AppletWidgetView), Namespace = "http://santedb.org/applet")]
    [JsonObject]
    [ExcludeFromCodeCoverage]
    public class AppletWidgetView
    {

        /// <summary>
        /// Gets or sets the type of view
        /// </summary>
        [XmlAttribute("type"), JsonProperty("type")]
        public AppletWidgetViewType ViewType { get; set; }

        /// <summary>
        /// Get or sets the demand for this view
        /// </summary>
        [XmlElement("demand"), JsonProperty("demand")]
        public List<String> Policies { get; set; }

    }

    /// <summary>
    /// Represents a widget. A widget is a special pointer which has a title and content which can be rendered
    /// in a container 
    /// </summary>
    [XmlType(nameof(AppletWidget), Namespace = "http://santedb.org/applet")]
    [JsonObject]
    [ExcludeFromCodeCoverage]
    public class AppletWidget : AppletAssetHtml
    {
        /// <summary>
        /// Gets or sets the scope where the widget can be used
        /// </summary>
        [XmlAttribute("context"), QueryParameter("context")]
        [JsonProperty("context")]
        public String Context { get; set; }

        /// <summary>
        /// Gets or sets the type of widget
        /// </summary>
        [XmlAttribute("type"), QueryParameter("type")]
        [JsonProperty("type")]
        public AppletWidgetType Type { get; set; }

        /// <summary>
        /// Gets or sets the type of widget
        /// </summary>
        [XmlAttribute("size"), QueryParameter("size")]
        [JsonProperty("size")]
        public AppletWidgetSize Size { get; set; }

        /// <summary>
        /// Alternate views
        /// </summary>
        [XmlArray("views"), XmlArrayItem("view"), JsonProperty("views")]
        public List<AppletWidgetView> AlternateViews { get; set; }

        /// <summary>
        /// Gets or sets the name of the widget
        /// </summary>
        [XmlAttribute("name")]
        [JsonProperty("name")]
        public String Name { get; set; }

        /// <summary>
        /// Gets or sets the guard conditions for the applet. These are conditions on the scope which must be true in order for the panel to be enabled and visible.
        /// </summary>
        [XmlElement("guard")]
        [JsonProperty("guard")]
        public List<String> Guard { get; set; }

        /// <summary>
        /// Gets or sets the controller
        /// </summary>
        [XmlElement("controller")]
        [JsonProperty("controller")]
        public String Controller { get; set; }

        /// <summary>
        /// Gets the main titles of the panel
        /// </summary>
        [XmlElement("description")]
        [JsonProperty("description")]
        public List<LocaleString> Description { get; set; }

        /// <summary>
        /// Gets or sets the icon file reference
        /// </summary>
        [XmlElement("icon")]
        [JsonProperty("icon")]
        public String Icon
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the order preference 
        /// </summary>
        [XmlAttribute("priority"), JsonProperty("priority")]
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the order preference 
        /// </summary>
        [XmlAttribute("maxStack"), JsonProperty("maxStack")]
        public int MaxStack { get; set; }

        /// <summary>
        /// Gets or sets the order preference 
        /// </summary>
        [XmlAttribute("order"), JsonProperty("order")]
        public int Order { get; set; }

        /// <summary>
        /// Color class
        /// </summary>
        [XmlAttribute("headerClass"), JsonProperty("headerClass")]
        public string ColorClass { get; set; }

        /// <summary>
        /// Gets the specified decription
        /// </summary>
        public String GetDescription(String language, bool returnNuetralIfNotFound = true)
        {
            var str = this.Description?.Find(o => o.Language == language);
            if (str == null && returnNuetralIfNotFound)
            {
                str = this.Description?.Find(o => o.Language == null);
            }

            return str?.Value;
        }
    }

}
