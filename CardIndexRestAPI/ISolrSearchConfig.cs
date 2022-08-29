using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SolrAPI
{
    public interface ISolrSearchConfig
    {
        public string SolrAddress { get; }
        public string CardsCollectionName { get; }
        public string ImagesCollectionName { get; }
        public int MaxReturnCount { get; }
        public double LongTermSearchRadiusKm { get; }
        public double ShortTermSearchRadiusKm { get; }
        public TimeSpan ShortTermLength { get; }
        public TimeSpan ReverseTimeGapLength { get; }
        public int SimilarityKnnTopK { get;}
    }
}
