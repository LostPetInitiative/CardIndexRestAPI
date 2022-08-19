using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SolrAPI
{
    /// <summary>
    /// LRU cache that does not grow over specified capacity. Also discards entries that are older (in therms of creation time) that specified threshold
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class LruStaleControlCache<T>
    {
        private readonly int capacity;
        private readonly TimeSpan staleThreshold;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="capacity"></param>
        /// <param name="staleTreshold"></param>
        public LruStaleControlCache(int capacity, TimeSpan staleTreshold)
        {
            this.capacity = capacity;
            this.staleThreshold = staleThreshold;
        }
    }
}
