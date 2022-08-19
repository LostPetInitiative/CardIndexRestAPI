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

        [EnableCors]
        [HttpPost("MatchedImagesSearch")]
        public async Task MatchedImagesSearch([FromBody] GetMatchesRequest request)
        {
            string requestHash = ((uint)request.GetHashCode()).ToString("X8");
            Trace.TraceInformation($"{requestHash}: Got request.");
            try
            {
                string featureDims = String.Join(",", Enumerable.Range(0, request.Features.Length).Select(idx => $"{request.FeaturesIdent}_{idx}_d"));
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
                    $"top(n={this.searchConfig.MaxReturnCount},having(select(search({this.searchConfig.ImagesCollectionName},q=\"animal:{request.Animal} AND {typeSearchTerm} AND (({shortTermSearchTerm})OR({longTermSearchTerm}))\",fl=\"id, event_time, {featureDims}\",sort=\"event_time asc\",qt=\"/export\"),id,cosineSimilarity(array({featureDims}), array({featuresTargetVal})) as similarity), gt(similarity, {this.searchConfig.SimilarityThreshold})),sort=\"similarity desc\")";
                //Trace.TraceInformation($"{requestHash}: Got request. Issuing: {solrFindLostRequest}");
                
                //solrFindLostRequest = "top(n=100,select(search(kashtankaimages,q=\"animal:Cat AND card_type:Lost AND ((event_time:[2019-06-19T21:00:00.0000000Z TO 2019-08-02T21:00:00.0000000Z] AND {!geofilt sfield=location pt=56.273015,43.93563 d=1000})OR(event_time:[ * TO 2019-08-02T21:00:00.0000000Z] AND {!geofilt sfield=location pt=56.273015,43.93563 d=20}))\",fl=\"id, event_time\",sort=\"event_time asc\",qt=\"/export\"),id,),sort=\"similarity desc\")";

                // To avoid "URL is too long" we pass the request inside POST body
                FormUrlEncodedContent requestContent = new FormUrlEncodedContent(new KeyValuePair<string, string>[] {
                    new KeyValuePair<string, string>("expr",solrFindLostRequest)
                }); ;

                await ProxyHttpPost(requestHash, this.solrImagesStreamingExpressionsURL, requestContent, Response);


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

            string queryStr =
                string.Join('&', requestParams.Select(kvp => $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

            string finalURL = $"{this.solrCardsSelectExpressionsURL}?{queryStr}";
            try {
                await ProxyHttpPost("latest cards request", finalURL,null, Response);
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
    }
}
