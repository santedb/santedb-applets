using Newtonsoft.Json;
using SanteDB.Core.Applets.ViewModel.Json;
using SanteDB.Core.Http;
using SanteDB.Core.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text;

namespace SanteDB.Core.Applets.ViewModel
{
    /// <summary>
    /// View model body serializer
    /// </summary>
    public class ViewModelBodySerializer : JsonBodySerializer
    {
        /// <inheritdoc/>
        public override string ContentType => "application/json+sdb-viewmodel";

        /// <inheritdoc/>
        public override object DeSerialize(Stream requestOrResponseStream, ContentType contentType, Type typeHint)
        {
            using (var sr = new StreamReader(requestOrResponseStream, System.Text.Encoding.GetEncoding(contentType.CharSet ?? "utf-8"), true, 2048, true))
            {
                var serializer = new JsonViewModelSerializer();
                return serializer.DeSerialize(sr, typeHint);
            }
        }

        /// <inheritdoc/>
        public override object GetSerializer(Type typeHint) => new JsonViewModelSerializer();

        /// <inheritdoc/>
        public override void Serialize(Stream requestOrResponseStream, object objectToSerialize, out ContentType contentType)
        {
            using (var sw = new StreamWriter(requestOrResponseStream, System.Text.Encoding.UTF8, 2048, true))
            {
                if (objectToSerialize is IdentifiedData identifiedData)
                {
                    var serializer = new JsonViewModelSerializer();
                    contentType = new ContentType($"{this.ContentType}; charset=utf-8");
                    serializer.Serialize(sw, identifiedData);
                }
                else
                {
                    base.Serialize(requestOrResponseStream, objectToSerialize, out contentType);
                }
            }
        }
    }
}
