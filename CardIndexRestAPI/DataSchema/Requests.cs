namespace CardIndexRestAPI.DataSchema
{
    public class Requests
    {
        /// <summary>
        /// POCO
        /// </summary>
        public class GetMatchesRequest {
            public double Lat { get; set; }
            public double Lon { get; set; }
            public string Animal { get; set; }
            public DateTime EventTime { get; set; }

            public string EventType { get; set; }

            public double[] Features { get; set; }
            public string FeaturesIdent { get; set; }


            public override int GetHashCode()
            {
                return
                    this.Lat.GetHashCode() ^ this.Lon.GetHashCode() ^
                    (this.Animal?.GetHashCode() ?? 0) ^
                    this.EventTime.GetHashCode() ^
                    this.EventType.GetHashCode() ^
                     this.Features.Select(f => f.GetHashCode()).Aggregate(0, (acc, elem) => acc ^ elem) ^
                    this.Features.Length.GetHashCode() ^
                    (this.FeaturesIdent?.GetHashCode() ?? 0);
            }
        }
    }
}
