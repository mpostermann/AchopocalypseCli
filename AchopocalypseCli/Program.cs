using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;
using RestSharp;
using SpreadsheetLight;
using System.Web;

namespace AchopocalypseCli
{
    public class Program
    {
        private static readonly IList<string> AchoChannelIds = new List<string>() {
            "UCCfnriDcUslGMUMX4Ctkyjg", // https://www.youtube.com/@GAMEacho
            "UCoBRHnYHaz-Xk1gFicgAKGw", // https://www.youtube.com/@a-chostaff6520
            "UCkXtcsyQ6g8coNrclPvt29w", // https://www.youtube.com/@zero3japan
        };

        private static readonly FileLogger Logger = new FileLogger($"LogOutput_{DateTime.Now.ToString("yyyy-dd-MM-HH-mm-ss")}.log");

        public static async Task<int> Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Invalid arguments. Usage: `AchopocalypseCli [INPUT_FILE_NAME] [OUTPUT_FILE_NAME]`");
                return -1;
            }

            try
            {
                var config = ParseConfig("config.json");

                var scores = ParseCsv(args[0]);
                var videoIds = GetAllVideoIds(scores);
                var youtubeData = await GetYoutubeData(config, videoIds);
                WriteOutput(scores, youtubeData, args[1]);

                return 0;
            }
            catch (Exception e)
            {
                Logger.WriteException(e);
                Console.Error.WriteLine("Fatal error occurred. Please see log for additional details.");
                Console.Error.WriteLine(e.ToString());
                return -1;
            }
        }

        private static Config ParseConfig(string filepath)
        {
            using (var file = File.OpenRead(filepath))
            {
                using (var reader = new StreamReader(file))
                {
                    var config = JsonConvert.DeserializeObject<Config>(reader.ReadToEnd());
                    if (config == null)
                    {
                        throw new Exception("Filed to load config.json");
                    }

                    return config;
                }
            }
        }

        private static IList<RsScoreModel> ParseCsv(string filepath)
        {
            var scoresList = new List<RsScoreModel>();

            Logger.WriteLine($"Begin reading csv file {filepath}");
            using (TextFieldParser parser = new TextFieldParser(filepath))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                bool headerRow = true;

                int row = 0;
                while (!parser.EndOfData)
                {
                    string[] fields = parser.ReadFields();
                    if (headerRow)
                    {
                        headerRow = false;
                    }
                    else
                    {
                        //Process row
                        var model = new RsScoreModel()
                        {
                            Id = int.Parse(fields[0]),
                            ScoreId = int.Parse(fields[1]),
                            User = fields[2],
                            Mode = fields[3],
                            Ship = fields[4],
                            Score = fields[5],
                            Comment = fields[6],
                            //Pic = fields[7],
                            Platform = fields[8],
                            Entered = fields[9],
                            Extra = fields[10],
                            Stage = fields[11],
                            Clear = fields[12],
                            Video = fields[13],
                            Private = fields[14],
                            Obsolete = fields[15],
                            //PlayedDate = fields[16],
                            Archive = fields[17]
                        };

                        if (model.Video.Contains("playlist") || model.Video.Contains("channel") || model.Video.Contains("youtube.com/@"))
                        {
                            Logger.WriteLine($"Skipping row {row}; video is a playlist or channel: {model.Video}");
                        }
                        else
                        {
                            scoresList.Add(model);
                            Logger.WriteLine($"Parsed row {row}, ScoreId: {model.ScoreId}, Video: {model.Video}");
                        }
                        row++;
                    }
                }
            }

            Logger.WriteLine($"Completed parsing .csv. {scoresList.Count} rows");
            return scoresList;
        }

        private static List<string> GetAllVideoIds(IList<RsScoreModel> scores)
        {
            Logger.WriteLine("Parsing Youtube IDs from video URLs...");

            var videoIds = new List<string>();
            int count = 0;
            foreach (var score in scores)
            {
                string id = ParseYoutubeId(score);
                if (!videoIds.Contains(id))
                {
                    Logger.WriteLine($"Get ID #{count}: {id}");
                    videoIds.Add(id);
                    count++;
                }
            }

            Logger.WriteLine($"Finished parsing Youtube IDs. {videoIds.Count} unique IDs");
            return videoIds;
        }

        private static string ParseYoutubeId(RsScoreModel score)
        {
            Uri uri;
            try
            {
                if (score.Video.StartsWith("youtube.com"))
                    uri = new Uri($"https://www.{score.Video}");
                else
                    uri = new Uri(score.Video);
            }
            catch
            {
                Logger.WriteLine($"Failed to parse as URI: {score.Video}");
                throw;
            }

            string id;
            if (uri.GetLeftPart(UriPartial.Path).Contains("youtube.com/watch"))
            {
                id = HttpUtility.ParseQueryString(uri.Query).Get("v");
                if (string.IsNullOrEmpty(id))
                {
                    id = HttpUtility.ParseQueryString(uri.Query).Get("amp;v");
                }
            }
            else if (uri.GetLeftPart(UriPartial.Path).Contains("youtube.com/live"))
            {
                id = uri.GetLeftPart(UriPartial.Path).Split("/").Last();
            }
            else
            {
                throw new Exception($"Unsure how to parse Youtube ID from URL: {score.Video}");
            }

            if (string.IsNullOrEmpty(id) || id.Length != 11)
                throw new Exception($"Something broke when parsing Id for URL {score.Video}. Id: {id}");
            return id;
        }

        private static async Task<IDictionary<string, YoutubeDataModel.Item>> GetYoutubeData(Config config, List<string> videoIds)
        {
            // Call the Youtube API
            var options = new RestClientOptions("https://www.googleapis.com/youtube/v3")
            {
                ThrowOnAnyError = false
            };

            var youtubeItems = new List<YoutubeDataModel.Item>();
            using (var client = new RestClient(options))
            {
                // Batch requests in groups of 50
                for (int i = 0; i < videoIds.Count; i = i + 50)
                {
                    int batchSize = Math.Min(50, videoIds.Count - i);
                    var request = new RestRequest("videos")
                        .AddQueryParameter("key", config.YoutubeApiKey)
                        .AddQueryParameter("part", "id,snippet") // Specify what fields we want back from the API
                        .AddQueryParameter("id", string.Join(",", videoIds.GetRange(i, batchSize))); // Specify the list of videos to query for

                    Logger.WriteLine($"Making Youtube API GET request for IDs {i} to {i+batchSize}");
                    var response = await client.GetAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.WriteLine($"Received a {response.StatusCode} error response. Body: {response.Content}");
                        throw response.ErrorException;
                    }
                    var youtubeData = JsonConvert.DeserializeObject<YoutubeDataModel>(response.Content);
                    if (youtubeData == null)
                    {
                        throw new Exception($"Failed to deserialize response. Body: {response.Content}");
                    }

                    youtubeItems.AddRange(youtubeData.items);
                }
            }

            // Map channels for each video
            Logger.WriteLine("Completed gathering responses from Youtube API");
            var dataMap = new Dictionary<string, YoutubeDataModel.Item>();
            int achoVideosCount = 0;
            int deadVideoCount = 0;
            foreach (string id in videoIds) 
            {
                var item = youtubeItems.FirstOrDefault(n => n.id == id);
                if (item == null)
                {
                    Logger.WriteLine($"Warning: Youtube ID {id} is not in the response from Youtube API. Link is likely dead.");
                    deadVideoCount++;
                }
                else
                {
                    dataMap.Add(id, item);

                    if (AchoChannelIds.Contains(item.snippet.channelId))
                    {
                        Logger.WriteLine($"Youtube ID {id} Belong to A-cho. ChannelId {item.snippet.channelId}, ChannelTitle {item.snippet.channelTitle}.");
                        achoVideosCount++;
                    }
                }
            }

            Logger.WriteLine($"Found {achoVideosCount} videos belonging to A-cho.");
            Logger.WriteLine($"Found {deadVideoCount} dead videos.");
            return dataMap;
        }

        private static void WriteOutput(IList<RsScoreModel> scores, IDictionary<string, YoutubeDataModel.Item> youtubeData, string filepath)
        {
            Logger.WriteLine("Writing output spreadsheet...");
            using (SLDocument doc = new SLDocument())
            {
                doc.RenameWorksheet(SLDocument.DefaultFirstSheetName, "RestartSyndromeAchoVideos");

                // Write headers
                doc.SetCellValue(1, 1, "id");
                doc.SetCellValue(1, 2, "score_id");
                doc.SetCellValue(1, 3, "vid");
                doc.SetCellValue(1, 4, "youtube_video_id");
                doc.SetCellValue(1, 5, "youtube_video_title");
                doc.SetCellValue(1, 6, "youtube_channel_id");
                doc.SetCellValue(1, 7, "youtube_channel_title");
                doc.SetCellValue(1, 8, "youtube_video_description");
                doc.SetCellValue(1, 9, "user");
                doc.SetCellValue(1, 10, "mode");
                doc.SetCellValue(1, 11, "ship");
                doc.SetCellValue(1, 12, "score");
                doc.SetCellValue(1, 13, "com");
                doc.SetCellValue(1, 14, "platform");
                doc.SetCellValue(1, 15, "entered");
                doc.SetCellValue(1, 16, "extra");
                doc.SetCellValue(1, 17, "stage");
                doc.SetCellValue(1, 18, "clear");
                doc.SetCellValue(1, 19, "private");
                doc.SetCellValue(1, 20, "obsolete");
                doc.SetCellValue(1, 21, "archive");

                // Write rows
                int currentRow = 2;
                foreach (var score in scores)
                {
                    var id = ParseYoutubeId(score);
                    var item = YoutubeDataModel.Item.DeadItem;
                    if (youtubeData.ContainsKey(id))
                    {
                        item = youtubeData[id];
                    }

                    doc.SetCellValue(currentRow, 1, score.Id);
                    doc.SetCellValue(currentRow, 2, score.ScoreId);
                    doc.SetCellValue(currentRow, 3, score.Video);
                    doc.SetCellValue(currentRow, 4, item.id);
                    doc.SetCellValue(currentRow, 5, item.snippet.title);
                    doc.SetCellValue(currentRow, 6, item.snippet.channelId);
                    doc.SetCellValue(currentRow, 7, item.snippet.channelTitle);
                    doc.SetCellValue(currentRow, 8, item.snippet.description);
                    doc.SetCellValue(currentRow, 9, score.User);
                    doc.SetCellValue(currentRow, 10, score.Mode);
                    doc.SetCellValue(currentRow, 11, score.Ship);
                    doc.SetCellValue(currentRow, 12, score.Score);
                    doc.SetCellValue(currentRow, 13, score.Comment);
                    doc.SetCellValue(currentRow, 14, score.Platform);
                    doc.SetCellValue(currentRow, 15, score.Entered);
                    doc.SetCellValue(currentRow, 16, score.Extra);
                    doc.SetCellValue(currentRow, 17, score.Stage);
                    doc.SetCellValue(currentRow, 18, score.Clear);
                    doc.SetCellValue(currentRow, 19, score.Private);
                    doc.SetCellValue(currentRow, 20, score.Obsolete);
                    doc.SetCellValue(currentRow, 21, score.Archive);

                    currentRow++;
                }

                doc.SaveAs(filepath);
                Logger.WriteLine($"Process completed. Output written to ${filepath}");
            }
        }
    }
}
