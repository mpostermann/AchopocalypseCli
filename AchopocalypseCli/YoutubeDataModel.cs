namespace AchopocalypseCli
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class YoutubeDataModel
    {
        public string kind { get; set; }
        public string etag { get; set; }
        public List<Item> items { get; set; }

        public class Item
        {
            public static readonly Item DeadItem = new Item()
            {
                id = "DEAD",
                snippet = new Snippet()
                {
                    title = "DEAD"
                }
            };

            public string kind { get; set; }
            public string etag { get; set; }
            public string id { get; set; }
            public Snippet snippet { get; set; }
        }

        public class Snippet
        {
            public string channelId { get; set; }
            public string channelTitle { get; set; }
            public string title { get; set; }
            public string description { get; set; }
        }
    }
}
