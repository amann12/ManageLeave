using Newtonsoft.Json;
//using static NuGet.Client.ManagedCodeConventions;
using System.Net;

namespace CoreBotCLU.Utilities
{
    public class LeaveDetail
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        public string LeaveId { get; set; }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
