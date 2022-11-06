using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Web;
using static CardIndexRestAPI.DataSchema.Requests;
using System.Numerics;

namespace SolrAPI.Controllers
{
    [Route("")]
    [ApiController]
    public class SolrProxyController : ControllerBase
    {
        private readonly string solrCardsStreamingExpressionsURL;
        private readonly string solrCardsSelectExpressionsURL;
        private readonly string solrImagesStreamingExpressionsURL;
        private readonly string solrImagesSelectExpressionsURL;
        private readonly ISolrSearchConfig searchConfig;

        public SolrProxyController(ISolrSearchConfig solrAddress)
        {
            this.searchConfig = solrAddress;
            this.solrCardsStreamingExpressionsURL = $"{solrAddress.SolrAddress}/solr/{solrAddress.CardsCollectionName}/stream";
            this.solrCardsSelectExpressionsURL = $"{solrAddress.SolrAddress}/solr/{solrAddress.CardsCollectionName}/select";
            this.solrImagesStreamingExpressionsURL = $"{solrAddress.SolrAddress}/solr/{solrAddress.ImagesCollectionName}/stream";
            this.solrImagesSelectExpressionsURL = $"{solrAddress.SolrAddress}/solr/{solrAddress.ImagesCollectionName}/select";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="requestID">How to identify the request in logs</param>
        /// <param name="requstURL"></param>
        /// <param name="response">Where to write proxied data</param>
        /// <returns></returns>
        private static async Task ProxyHttpPost(string requestID, string requstURL, HttpContent? content, HttpResponse response) {            
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(20);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            
            Trace.TraceInformation($"{requestID}: Issung request {requstURL}.");
            var responseData = await client.PostAsync(requstURL, content);
            Trace.TraceInformation($"{requestID}: Got reply with code {responseData.StatusCode}. Proxying...");
            
            response.StatusCode = (int)responseData.StatusCode;
            if (responseData.Headers.Contains(HttpResponseHeader.ContentType.ToString()))
                response.ContentType = responseData.Headers.GetValues(HttpResponseHeader.ContentType.ToString()).First();
            foreach(var h in responseData.Headers)
                Trace.TraceInformation($"{h.Key} {h.Value.Aggregate((acc,s) => $"{acc} {s}")}");

            var bytes = await responseData.Content.ReadAsByteArrayAsync();

            response.ContentLength = bytes.Length;
            Trace.TraceInformation($"Content length is {bytes.Length}");

            await response.StartAsync();

            var outStream = response.BodyWriter.AsStream();
            await outStream.WriteAsync(bytes);
            await response.BodyWriter.FlushAsync();
            await response.BodyWriter.CompleteAsync();
            await response.CompleteAsync();            
            await outStream.DisposeAsync();
        }

        /*
        [EnableCors]
        [HttpPost("MatchedCardsSearch")]
        public async Task MatchedCardsSearch([FromBody]GetMatchesRequest request)
        {
            string requestHash = ((uint)request.GetHashCode()).ToString("X8");
            Trace.TraceInformation($"{requestHash}: Got request.");
            try
            {
                string featureDims = String.Join(",", Enumerable.Range(0, request.Features.Length).Select(idx => $"features_{request.FeaturesIdent}_{idx}_d"));
                string featuresTargetVal = String.Join(',', request.Features);
                
                DateTime shortTermSearchStart = request.EventType switch
                {
                    "Found" => (request.EventTime - this.searchConfig.ShortTermLength), // looking back in time
                    "Lost" => (request.EventTime - this.searchConfig.ReverseTimeGapLength),
                    _ => throw new ArgumentException($"Unknown EventType: {request.EventType}")
                };

                DateTime shortTermSearchEnd = request.EventType switch
                {
                    "Found" => (request.EventTime + this.searchConfig.ReverseTimeGapLength), 
                    "Lost" => (request.EventTime + this.searchConfig.ShortTermLength),
                    _ => throw new ArgumentException($"Unknown EventType: {request.EventType}")
                };

                string toISO(DateTime d) => d.ToString("o", System.Globalization.CultureInfo.InvariantCulture);

                string shortTermTimeSpec = $"event_time:[{toISO(shortTermSearchStart)} TO {toISO(shortTermSearchEnd)}]";
                string shortTermSpaceSpec = $"{{!geofilt sfield=location pt={request.Lat},{request.Lon} d={this.searchConfig.ShortTermSearchRadiusKm}}}";
                string shortTermSearchTerm = $"{shortTermTimeSpec} AND {shortTermSpaceSpec}";

                string longTermTimeSpec = request.EventType switch
                {
                    "Found" => $"event_time:[ * TO {toISO(request.EventTime + this.searchConfig.ReverseTimeGapLength)}]",
                    "Lost" => $"event_time:[{toISO(request.EventTime - this.searchConfig.ReverseTimeGapLength)} TO *]",
                    _ => throw new ArgumentException($"Unknown EventType: {request.EventType}")
                };
                string longTermSpaceSpec = $"{{!geofilt sfield=location pt={request.Lat},{request.Lon} d={this.searchConfig.LongTermSearchRadiusKm}}}";
                string longTermSearchTerm = $"{longTermTimeSpec} AND {longTermSpaceSpec}";

                string typeSearchTerm = request.EventType switch
                {
                    "Found" => "card_type:Lost",
                    "Lost" => "card_type:Found",
                    _ => throw new ArgumentException($"Unknown EventType: {request.EventType}")
                };


                string solrFindLostRequest =
                    $"top(n={this.searchConfig.MaxReturnCount},having(select(search({this.searchConfig.CardsCollectionName},q=\"animal:{request.Animal} AND {typeSearchTerm} AND (({shortTermSearchTerm})OR({longTermSearchTerm}))\",fl=\"id, event_time, {featureDims}\",sort=\"event_time asc\",qt=\"/export\"),id,cosineSimilarity(array({featureDims}), array({featuresTargetVal})) as similarity), gt(similarity, {this.searchConfig.SimilarityThreshold})),sort=\"similarity desc\")";
                //Trace.TraceInformation($"{requestHash}: Got request. Issuing: {solrFindLostRequest}");

                //string requestExprEncoded = HttpUtility.UrlEncode(solrFindLostRequest);

                FormUrlEncodedContent requestContent = new FormUrlEncodedContent(new KeyValuePair<string, string>[] {
                    new KeyValuePair<string, string>("expr",solrFindLostRequest)
                }); ;                                

                await ProxyHttpPost(requestHash, this.solrCardsStreamingExpressionsURL, requestContent, Response);


                Trace.TraceInformation($"{requestHash}: Transmitted successfully");
            }
            catch (Exception err)
            {
                Trace.TraceError($"{requestHash}: Exception occurred {err}");
                string errorMsg = err.ToString();
                Response.StatusCode = 500;
                Response.ContentLength = ASCIIEncoding.Unicode.GetByteCount(errorMsg);
                await Response.WriteAsync(errorMsg);
                await Response.CompleteAsync();
            }
        }

        */

        [EnableCors]
        [HttpGet("Health")]
        public IActionResult Health() {
            return Ok("Works!");
        }

        [EnableCors]
        [HttpPost("MatchedImagesSearch")]
        public async Task MatchedImagesSearch([FromBody] GetMatchesRequest request)
        {
            string requestHash = ((uint)request.GetHashCode()).ToString("X8");
            Trace.TraceInformation($"{requestHash}: Got request.");
            try
            {

                var features = request.Features.ToArray(); //.Take(900).ToArray();
                double norm = Math.Sqrt(features.Sum(x => x * x));
                features = features.Select(x => x / norm).ToArray();

                Trace.TraceInformation($"Feature length is {features.Length}");
                string featureDims = String.Join(",", Enumerable.Range(0, features.Length).Select(idx => $"{request.FeaturesIdent}_{idx}_d"));
                //string featuresTargetVal = String.Join(',', features.Select(d => Math.Round(d,3)));
                string featuresTargetVal = String.Join(',', features);

                DateTime shortTermSearchStart = request.EventType switch
                {
                    "Found" => (request.EventTime - this.searchConfig.ShortTermLength), // looking back in time
                    "Lost" => (request.EventTime - this.searchConfig.ReverseTimeGapLength),
                    _ => throw new ArgumentException($"Unknown EventType: {request.EventType}")
                };

                DateTime shortTermSearchEnd = request.EventType switch
                {
                    "Found" => (request.EventTime + this.searchConfig.ReverseTimeGapLength),
                    "Lost" => (request.EventTime + this.searchConfig.ShortTermLength),
                    _ => throw new ArgumentException($"Unknown EventType: {request.EventType}")
                };

                string toISO(DateTime d) => d.ToString("o", System.Globalization.CultureInfo.InvariantCulture);

                string shortTermTimeSpec = $"event_time:[{toISO(shortTermSearchStart)} TO {toISO(shortTermSearchEnd)}]";
                string shortTermSpaceSpec = $"{{!geofilt sfield=location pt={request.Lat},{request.Lon} d={this.searchConfig.ShortTermSearchRadiusKm}}}";
                string shortTermSearchTerm = $"{shortTermTimeSpec} AND {shortTermSpaceSpec}";

                string longTermTimeSpec = request.EventType switch
                {
                    "Found" => $"event_time:[ * TO {toISO(request.EventTime + this.searchConfig.ReverseTimeGapLength)}]",
                    "Lost" => $"event_time:[{toISO(request.EventTime - this.searchConfig.ReverseTimeGapLength)} TO *]",
                    _ => throw new ArgumentException($"Unknown EventType: {request.EventType}")
                };
                string longTermSpaceSpec = $"{{!geofilt sfield=location pt={request.Lat},{request.Lon} d={this.searchConfig.LongTermSearchRadiusKm}}}";
                string longTermSearchTerm = $"{longTermTimeSpec} AND {longTermSpaceSpec}";

                List<string> additionalFilter = new List<string>();
                if (request.FilterFar ?? false)
                    additionalFilter.Add($"({longTermSearchTerm})");
                if (request.FilterLongAgo ?? false)
                    additionalFilter.Add($"({shortTermSearchTerm})");
                string additionalFiltersStr = String.Join(" OR ", additionalFilter);

                string typeFilterTerm = request.EventType switch // this one inverts the specification
                {
                    "Found" => "card_type:Lost",
                    "Lost" => "card_type:Found",
                    _ => throw new ArgumentException($"Unknown EventType: {request.EventType}")
                };

                string animalFilterTerm = request.Animal switch
                {
                    "Cat" => "animal:Cat",
                    "Dog" => "animal:Dog",
                    _ => throw new ArgumentException($"Unknown Animal: {request.Animal}")
                };

                string cardTypeAndAnimalFilter = $"{typeFilterTerm} AND {animalFilterTerm}";

                // example from docs: https://solr.apache.org/guide/solr/latest/query-guide/dense-vector-search.html#query-time
                // { !knn f = vector topK = 10}[1.0, 2.0, 3.0, 4.0]

                const string embeddingName = "calvin_zhirui_embedding";

                string similaritySearchExpr = $"{{!knn f={embeddingName} topK={searchConfig.SimilarityKnnTopK}}}[{featuresTargetVal}]";


                Trace.TraceInformation($"sim search expr: ${similaritySearchExpr}");

                List<KeyValuePair<string, string>> requestParams = new List<KeyValuePair<string, string>>(new KeyValuePair<string, string>[] {
                    //new KeyValuePair<string, string>("expr",solrFindLostRequest)
                    new KeyValuePair<string, string>("q",similaritySearchExpr),
                    new KeyValuePair<string, string>("fl",$"id,{embeddingName}"),
                    new KeyValuePair<string, string>("fq",cardTypeAndAnimalFilter),
                    new KeyValuePair<string, string>("rows",searchConfig.MaxReturnCount.ToString())
                });

                if (!string.IsNullOrEmpty(additionalFiltersStr))
                    requestParams.Add(new KeyValuePair<string, string>("fq", additionalFiltersStr));

                // To avoid "URL is too long" we pass the request inside POST body
                FormUrlEncodedContent requestContent = new FormUrlEncodedContent(requestParams);
                

                //return NotFound();

                await ProxyHttpPost(requestHash, this.solrImagesSelectExpressionsURL, requestContent, Response);


                Trace.TraceInformation($"{requestHash}: Transmitted successfully");
            }
            catch (Exception err)
            {
                Trace.TraceError($"{requestHash}: Exception occurred {err}");
                string errorMsg = err.ToString();
                Response.StatusCode = 500;
                Response.ContentLength = ASCIIEncoding.Unicode.GetByteCount(errorMsg);
                await Response.WriteAsync(errorMsg);
                await Response.CompleteAsync();
            }
        }


        [EnableCors]
        [HttpGet("LatestCards")]
        public async Task LatestCards([FromQuery]int maxCardsCount=10, [FromQuery] string cardType=null) {
            maxCardsCount = Math.Min(1000, maxCardsCount);

            string typeConstraint = cardType?.ToLowerInvariant() switch
            {
                "lost" => "card_type:Lost",
                "found" => "card_type:Found",
                _ => string.Empty
            };

            Trace.TraceInformation($"Fetching no more than {maxCardsCount} latest cards (card type constraint: {typeConstraint})");

            Dictionary<string, string> requestParams = new Dictionary<string, string>();
            requestParams.Add("q","*:*");
            requestParams.Add("sort", "card_creation_time desc");
            requestParams.Add("fl", "id");
            requestParams.Add("rows", $"{maxCardsCount}");
            if (!string.IsNullOrEmpty(typeConstraint)) {
                requestParams.Add("fq", typeConstraint);
            }
            
            FormUrlEncodedContent requestContent = new FormUrlEncodedContent(requestParams);
            
            try {
                await ProxyHttpPost("latest cards request", this.solrCardsSelectExpressionsURL, requestContent, Response);
            }
            catch (Exception err)
            {
                string errorMsg = $"Exception occurred during latest cards fetch: {err}";
                Trace.TraceError(errorMsg);
                Response.StatusCode = 500;
                Response.ContentLength = ASCIIEncoding.Unicode.GetByteCount(errorMsg);
                await Response.WriteAsync(errorMsg);
                await Response.CompleteAsync();
            }
        }

        [EnableCors]
        [HttpGet("RecentCrawledStats")]
        public async Task RecentCrawledStats([FromQuery] string cardsNamespace, [FromQuery] RecentStatsMode mode = RecentStatsMode.Days)
        {
            string facettingStartStr = mode switch
            {
                RecentStatsMode.Days => "NOW-7DAY/DAY",
                RecentStatsMode.Months => "NOW-12MONTH/MONTH",
                _ => string.Empty
            };
            string facettingRangeStr = mode switch
            {
                RecentStatsMode.Days => "+1DAY",
                RecentStatsMode.Months => "+1MONTH",
                _ => string.Empty
            };

            Trace.TraceInformation($"Fetching statistics for ns \"{cardsNamespace.Replace(Environment.NewLine, "")}\" over recent {mode}(s)");

            Dictionary<string, string> requestParams = new Dictionary<string, string>();
            requestParams.Add("q", "*:*");
            requestParams.Add("fl", "id, card_creation_time");
            requestParams.Add("fq", $"id:/{cardsNamespace}.*/");
            requestParams.Add("facet", "true");
            requestParams.Add("facet.range", "card_creation_time");
            requestParams.Add("facet.range.start", facettingStartStr);
            requestParams.Add("facet.range.end", "NOW");
            requestParams.Add("facet.range.gap", facettingRangeStr);

            
            FormUrlEncodedContent requestContent = new FormUrlEncodedContent(requestParams);

            try
            {
                await ProxyHttpPost("recent stats", this.solrCardsSelectExpressionsURL, requestContent, Response);
            }
            catch (Exception err)
            {
                string errorMsg = $"Exception occurred during recent stats fetch: {err}";
                Trace.TraceError(errorMsg);
                Response.StatusCode = 500;
                Response.ContentLength = ASCIIEncoding.Unicode.GetByteCount(errorMsg);
                await Response.WriteAsync(errorMsg);
                await Response.CompleteAsync();
            }
        }
    }

    public enum RecentStatsMode { Days, Months}
}
