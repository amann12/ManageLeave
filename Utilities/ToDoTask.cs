using Newtonsoft.Json;
using static NuGet.Client.ManagedCodeConventions;
using System.Net;

namespace ToDoBot.Utilities
{
    public class ToDoTask
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        public string Task { get; set; }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
