using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SolrAPI
{
    public class StaticSolrSearchConfig : ISolrSearchConfig
    {
        public string SolrAddress { get; private set; }

        public string CardsCollectionName { get; private set; }

        public string ImagesCollectionName { get; private set; }

        public int MaxReturnCount { get; private set; }

        public double LongTermSearchRadiusKm { get; private set; }

        public double ShortTermSearchRadiusKm { get; private set; }

        public TimeSpan ShortTermLength { get; private set; }

        public int SimilarityKnnTopK { get; private set; }

        public TimeSpan ReverseTimeGapLength { get; private set; }

        public StaticSolrSearchConfig(
            string address,
            string cardsCollectionName,
            string imagesCollectionName,
            int maxReturnCount,
            double longTermSearchRadiusKm,
            double shortTermSearchRadiusKm,
            TimeSpan shortTermLength,
            int similarityKnnTopK,
            TimeSpan reverseTimeGapLength
            ) {
            this.SolrAddress = address;
            this.CardsCollectionName = cardsCollectionName;
            this.ImagesCollectionName = imagesCollectionName;
            this.MaxReturnCount = maxReturnCount;
            this.LongTermSearchRadiusKm = longTermSearchRadiusKm;
            this.ShortTermSearchRadiusKm = shortTermSearchRadiusKm;
            this.ShortTermLength = shortTermLength;
            this.SimilarityKnnTopK = similarityKnnTopK;
            this.ReverseTimeGapLength = reverseTimeGapLength;
        }
    }
}
