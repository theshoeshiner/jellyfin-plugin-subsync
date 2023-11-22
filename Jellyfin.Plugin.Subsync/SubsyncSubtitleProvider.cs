using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Jellyfin.Plugin.SubSync
{

    public class SubsyncSubtitleProvider : ISubtitleProvider
    {
        private const string SrtExtension = "srt";
        private const string SubSyncExeFile = "subsync.exe";
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
            logger.LogDebug("GetSubtitles: {Id}", id);

            var parts = id.Split(seperator);
            var index = int.Parse(parts[1]);

            var item = manager.GetItemsResult(new InternalItemsQuery
            { ItemIds = new Guid[] { Guid.Parse(parts[0]) } }).Items[0];

            var subtitle = item.GetMediaStreams()[index];

            var backupPath = subtitle.Path + ".original";

            for (var i = 0; File.Exists(backupPath); i++)
            {
                backupPath = Path.ChangeExtension(backupPath, "original" + i);
            }

            logger.LogDebug("writing backup file: {0}", backupPath);

            using (var originalStream = new FileStream(subtitle.Path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                using (var backupStream = new FileStream(backupPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                {
                    originalStream.CopyTo(backupStream);
                }
            }

            var newPath = subtitle.Path;

            if (!subtitle.Path.EndsWith(SrtExtension, StringComparison.OrdinalIgnoreCase))
            {
                newPath = string.Concat(subtitle.Path.AsSpan(0, subtitle.Path.Length - 3), SrtExtension);
                logger.LogDebug("writing converted file: {0}", newPath);
                // need to convert to srt
                var converted = await subtitleEncoder.GetSubtitles(item, null, subtitle.Index, SrtExtension, 0, 0, true, CancellationToken.None).ConfigureAwait(false);
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
                // dont need to convert file
            }

            RunSubSync(item, subtitle, backupPath, newPath);

            // dont actually return anything since we overwrote the file
            return new SubtitleResponse();
        }

        public Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {

            logger.LogDebug("Search: {MediaPath}", request.MediaPath);

            var content = request.ContentType;
            var path = request.MediaPath;
            var result = manager.GetItemsResult(new InternalItemsQuery
            {
                Path = path
            });

            var results = new List<RemoteSubtitleInfo>();

            foreach (var item in result.Items)
            {
                logger.LogDebug("Item: {Item}", item);

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
                        Format = SrtExtension,
                    };
                    results.Add(info);
                });
            }

            return Task.FromResult(results.AsEnumerable());
        }


        private void RunSubSync(BaseItem item, MediaStream stream, string inputPath, string outputPath)
        {

            var config = Plugin.Instance!.Configuration;
            var subSyncExePath = config.SubSyncPath;
            if (File.GetAttributes(subSyncExePath).HasFlag(FileAttributes.Directory))
            {
                subSyncExePath = Path.Combine(subSyncExePath, SubSyncExeFile);
            }

            // string logPath = Path.Combine(Path.GetDirectoryName(outputPath), "subsync.log");

            logger.LogDebug("subSyncExePath: {0}", subSyncExePath);

            // FIXME we have to assume the audio language is the same as the sub language
            // would be nice to be able to specify this
            var args = "--cli sync" +
                " --sub \"" + inputPath + "\"" +
                " --ref \"" + item.Path + "\"" +
                " --sub-lang " + stream.Language +
                " --ref-lang " + stream.Language +
                " --out \"" + outputPath + "\"" +
                //" --logfile \"" + logPath + "\"" +
                //" --loglevel INFO" +
                " --verbose 3" +
                " --overwrite";

            var p = new ProcessStartInfo(subSyncExePath, args);
            p.UseShellExecute = false;
            p.RedirectStandardError = true;
            p.RedirectStandardOutput = true;
            p.CreateNoWindow = true;
            p.ErrorDialog = false;

            logger.LogDebug("args: {0}", args);

            using (var process = Process.Start(p))
            {
                /*process.OutputDataReceived += (sender, args) =>
                {
                    logger.LogDebug("OutputDataReceived");
                    string s = args.Data;
                    logger.LogDebug("OutputDataReceived: {0}", s);

                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    logger.LogDebug("ErrorDataReceived");
                    string s = args.Data;
                    logger.LogDebug("ErrorDataReceived: {0}", s);

                };

                process.BeginErrorReadLine();
                process.BeginOutputReadLine();*/

                var error = process.StandardError.ReadToEnd();
                var output = process.StandardOutput.ReadToEnd();

                logger.LogDebug("output: {0}", output);
                logger.LogDebug("error: {0}", error);

                //The output result
                process.WaitForExit();

            }

            logger.LogDebug("subsync process done");

        }

    }


}
