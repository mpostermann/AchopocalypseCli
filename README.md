# AchopocalypseCli
Simple one-time use command-line tool to identify videos from RestartSyndrome that need to be backed up due to A-cho's Youtube channels shutting down.

# Setup and usage
1. Obtain a Google API key with access to the [Youtube Data API](https://developers.google.com/youtube/v3/docs/videos/list)
2. Create a config.json file with the following format:
```
{
  "YoutubeApiKey": <YOUR_GOOGLE_API_KEY>
}
```
3. Run the program. `.\Achopocalypse.exe <INPUT_FILE.csv> <OUTPUT_FILE.xlsx>`
