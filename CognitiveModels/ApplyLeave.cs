// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.Bot.Builder;
using Microsoft.BotBuilderSamples.Clu;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.BotBuilderSamples
{
    /// <summary>
    /// An <see cref="IRecognizerConvert"/> implementation that provides helper methods and properties to interact with
    /// the CLU recognizer results.
    /// </summary>
    public class ApplyLeave : IRecognizerConvert
    {
        public enum Intent
        {
            //[EnumMember(Value = "Cancel Leave")]
            CancelLeave,
            //[EnumMember(Value = "Check Leave Balance")]
            CheckBalance,
            //[EnumMember(Value = "Request Leave")]
            RequestLeave,
            None
        }

        public string Text { get; set; }

        public string AlteredText { get; set; }

        public Dictionary<Intent, IntentScore> Intents { get; set; }

        public CluEntities Entities { get; set; }

        public IDictionary<string, object> Properties { get; set; }

        public void Convert(dynamic result)
        {
            var jsonResult = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var app = JsonConvert.DeserializeObject<ApplyLeave>(jsonResult);

            Text = app.Text;
            AlteredText = app.AlteredText;
            Intents = app.Intents;
            Entities = app.Entities;
            Properties = app.Properties;
        }

        public (Intent intent, double score) GetTopIntent()
        {
            var maxIntent = Intent.None;
            var max = 0.0;
            foreach (var entry in Intents)
            {
                if (entry.Value.Score > max)
                {
                    maxIntent = entry.Key;
                    max = entry.Value.Score.Value;
                }
            }

            return (maxIntent, max);
        }

        public class CluEntities
        {
            public CluEntity[] Entities;

            //public CluEntity[] GetFromCityList() => Entities.Where(e => e.Category == "fromCity").ToArray();
            public CluEntity[] GetLeaveTypeList() => Entities.Where(e => e.Category=="LeaveType").ToArray();
            public string GetLeaveType() => GetLeaveTypeList().FirstOrDefault()?.Text;

            public CluEntity[] GetLeaveDayList() => Entities.Where(e => e.Category == "Day").ToArray();
            public string GetLeaveDay() => GetLeaveDayList().FirstOrDefault()?.Text;
            //public CluEntity[] GetToCityList() => Entities.Where(e => e.Category == "toCity").ToArray();

            //public CluEntity[] GetFlightDateList() => Entities.Where(e => e.Category == "flightDate").ToArray();

            //public string GetFromCity() => GetFromCityList().FirstOrDefault()?.Text;

            //public string GetToCity() => GetToCityList().FirstOrDefault()?.Text;

            //public string GetFlightDate() => GetFlightDateList().FirstOrDefault()?.Text;
        }
    }
}
