using SolrAPI;
using System.Diagnostics;

namespace CardIndexRestAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    builder =>
                    {
                        builder.WithOrigins("http://localhost:3000");
                        //builder.WithHeaders("Accept: application/json", "Content-Type: application/json");
                        builder.AllowAnyHeader();
                        builder.AllowAnyMethod();
                    });
            });

            string solrUrl = Environment.GetEnvironmentVariable("SOLR_URL");
            string cardsCollectionName = Environment.GetEnvironmentVariable("CARDS_COLLECTION_NAME");
            string imagesCollectionName = Environment.GetEnvironmentVariable("IMAGES_COLLECTION_NAME");
            if (solrUrl == null || cardsCollectionName == null || imagesCollectionName == null)
            {
                Trace.TraceWarning("SOLR_URL or CARDS_COLLECTION_NAME or IMAGES_COLLECTION_NAME env var is not found. ");
                Environment.Exit(1);
            }
            else
            {
                int maxReturnCount = int.Parse(Environment.GetEnvironmentVariable("MAX_RETURN_COUNT") ?? "100");
                Trace.TraceInformation($"MAX_RETURN_COUNT: {maxReturnCount}");

                double longTermSearchRadiusKm = double.Parse(Environment.GetEnvironmentVariable("LONG_TERM_SEARCH_RADIUS_KM") ?? "20.0");
                Trace.TraceInformation($"LONG_TERM_SEARCH_RADIUS_KM: {longTermSearchRadiusKm}");

                double shortTermSearchRadiusKm = double.Parse(Environment.GetEnvironmentVariable("SHORT_TERM_SEARCH_RADIUS_KM") ?? "1000.0");
                Trace.TraceInformation($"SHORT_TERM_SEARCH_RADIUS_KM: {shortTermSearchRadiusKm}");

                TimeSpan shortTermLength = TimeSpan.FromDays(int.Parse(Environment.GetEnvironmentVariable("SHORT_TERM_LENGTH_DAYS") ?? "30"));
                Trace.TraceInformation($"SHORT_TERM_LENGTH_DAYS: {shortTermLength}");

                TimeSpan reverseTimeGapLength = TimeSpan.FromDays(int.Parse(Environment.GetEnvironmentVariable("REVERSE_TIME_GAP_LENGTH_DAYS") ?? "14"));
                Trace.TraceInformation($"REVERSE_TIME_GAP_LENGTH_DAYS: {reverseTimeGapLength}");

                double similarityThreshold = double.Parse(Environment.GetEnvironmentVariable("SIMILARITY_THRESHOLD") ?? "0.1");
                Trace.TraceInformation($"SIMILARITY_THRESHOLD: {similarityThreshold}");

                //builder.Services.AddSingleton(typeof(IPhotoStorage), storage);
                builder.Services.AddSingleton(typeof(ISolrSearchConfig),
                    new StaticSolrSearchConfig(solrUrl, cardsCollectionName, imagesCollectionName,
                        maxReturnCount,
                        longTermSearchRadiusKm,
                        shortTermSearchRadiusKm,
                        shortTermLength,
                        similarityThreshold,
                        reverseTimeGapLength
                    ));
            }

            var app = builder.Build();            

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseCors();


            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}