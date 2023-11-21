using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Subsync.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;
using static System.Net.Mime.MediaTypeNames;

namespace Jellyfin.Plugin.Subsync
{

    public class SubsyncSubtitleProvider : ISubtitleProvider
    {
        private ILogger<SubsyncSubtitleProvider> logger;
        private ILibraryManager manager;
        private ISubtitleManager subtitleManager;
        private ISubtitleEncoder subtitleEncoder;
        private string seperator = "|";

        public SubsyncSubtitleProvider(ILogger<SubsyncSubtitleProvider> logger, ILibraryManager manager, ISubtitleManager subtitleManager, ISubtitleEncoder subtitleEncoder)
        {
            this.logger = logger;
            this.manager = manager;
            this.subtitleManager = subtitleManager;
            this.subtitleEncoder = subtitleEncoder;

        }

        public string Name => "SubSync Subtitles";

        public IEnumerable<VideoContentType> SupportedMediaTypes => new[] { VideoContentType.Episode, VideoContentType.Movie };

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            logger.LogInformation("GetSubtitles: {Id}", id);

            var parts = id.Split(seperator);
            var index = int.Parse(parts[1]);

            var item = manager.GetItemsResult(new InternalItemsQuery
            {
                ItemIds = new Guid[] { Guid.Parse(parts[0]) }
            }).Items.First();

            var subtitle = item.GetMediaStreams()[index];


            //subtitleManager

            //FileStream originalFile = new FileStream(subtitle.Path, FileMode.Open, FileAccess.Read, FileShare.None);

            //FileStream backupFile = new FileStream(subtitle.Path+".original", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            ///string tempPath = Path.GetTempFileName();

            var backupPath = subtitle.Path + ".original";
            logger.LogInformation("writing backup file: {0}", backupPath);
            using (var originalStream = new FileStream(subtitle.Path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                using (var backupStream = new FileStream(backupPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                {
                    originalStream.CopyTo(backupStream);
                }
            }

            var newPath = subtitle.Path;

            if (!subtitle.Path.EndsWith("srt", StringComparison.OrdinalIgnoreCase))
            {
                newPath = string.Concat(subtitle.Path.AsSpan(0, subtitle.Path.Length - 3), "srt");
                logger.LogInformation("writing converted file: {0}", newPath);
                //need to convert to srt
                var converted = await subtitleEncoder.GetSubtitles(item, null, subtitle.Index, "srt", 0, 0, true, CancellationToken.None);
                using (var newPathStream = File.Create(newPath))
                {
                    using (converted)
                    {
                        converted.CopyTo(newPathStream);
                    }
                }
            }
            else
            {
                //dont need to convert file
            }

            RunSubSync(item, subtitle, backupPath, newPath);

            //dont actually return anything 
            return new SubtitleResponse();
        }

        public Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {

            logger.LogInformation("Search: {MediaPath}", request.MediaPath);

            var content = request.ContentType;
            var path = request.MediaPath;
            var result = manager.GetItemsResult(new InternalItemsQuery
            {
                Path = path
            });



            var results = new List<RemoteSubtitleInfo>();

            foreach (var item in result.Items)
            {
                logger.LogInformation("Item: {Item}", item);


                item.GetMediaStreams().FindAll(s => s.Type == MediaStreamType.Subtitle && s.Language == request.Language).ToList().ForEach(subtitle =>
                {

                    var fileName = Path.GetFileName(subtitle.Path);


                    var info = new RemoteSubtitleInfo
                    {
                        Id = item.Id + seperator + subtitle.Index,
                        Name = "Subsync: " + fileName,
                        IsHashMatch = true,
                        ProviderName = Name,
                        ThreeLetterISOLanguageName = subtitle.Language,
                        Comment = "Run SubSync on: " + subtitle.Path,
                        //DownloadCount = 10000000,

                        Format = "srt",
                    };
                    results.Add(info);
                });
            }


            return Task.FromResult(results.AsEnumerable());
        }


        private void RunSubSync(BaseItem item, MediaStream stream, string inputPath, string outputPath)
        {

            PluginConfiguration config = Plugin.Instance!.Configuration;
            var subSyncExePath = config.SubSyncPath;
            if (File.GetAttributes(subSyncExePath).HasFlag(FileAttributes.Directory))
            {
                subSyncExePath = Path.Combine(subSyncExePath, "subsync.exe");
            }

            logger.LogInformation("subSyncExePath: {0}", subSyncExePath);

            using (var pProcess = new System.Diagnostics.Process())
            {

                pProcess.StartInfo.FileName = subSyncExePath;
                var args = "--cli sync" +
                    " --sub \"" + inputPath + "\"" +
                    " --ref \"" + item.Path + "\"" +
                    " --sub-lang " + stream.Language +
                    " --ref-lang " + stream.Language +
                    " --out \"" + outputPath + "\"" +
                    " --overwrite";

                logger.LogWarning("args: {0}", args);
                pProcess.StartInfo.Arguments = args;

                pProcess.StartInfo.UseShellExecute = false;
                pProcess.StartInfo.RedirectStandardOutput = true;
                pProcess.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                pProcess.StartInfo.CreateNoWindow = true; //not diplay a windows
                /*pProcess.Start();
                string output = pProcess.StandardOutput.ReadToEnd(); //The output result
                pProcess.WaitForExit();

                logger.LogInformation("output: {0}", output);*/
            }
        }

    }


}
