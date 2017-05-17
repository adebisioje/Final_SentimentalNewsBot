using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmotionalNewsBot.Models
{
    public class BingNews
    {
        public string readLink { get; set; }
        public int totalEstimatedMatches { get; set; }
        public NewsResult[] value { get; set; }
    }

    public class NewsResult
    {
        public string name { get; set; }
        public string url { get; set; }
        public Image image { get; set; }
        public string description { get; set; }
        public Provider[] provider { get; set; }
        public DateTime datePublished { get; set; }
    }

    public class Image
    {
        public Thumbnail thumbnail { get; set; }
    }

    public class Thumbnail
    {
        public string contentUrl { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }

    public class Provider
    {
        public string name { get; set; }
    }
}



