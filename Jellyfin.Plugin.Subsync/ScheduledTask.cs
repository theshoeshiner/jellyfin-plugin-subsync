using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Subsync.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Subsync
{
    internal class ScheduledTask : IScheduledTask
    {
        private ILogger<ScheduledTask> logger;
        private ILibraryManager libraryManager;

        public ScheduledTask(ILogger<ScheduledTask> logger, ILibraryManager libraryManager)
        {
            this.logger = logger;
            this.libraryManager = libraryManager;
        }

        public string Name => "Subsync Runner";

        public string Key => "subsync-task";

        public string Description => "Subsync Runner";

        public string Category => "Library";

        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {

            var config = Plugin.Instance!.Configuration;
            var allVideos = libraryManager.GetItemList(new InternalItemsQuery
            {
                MediaTypes = new[] { MediaType.Video }
            }
            );

            logger.LogWarning("all videos: {0}", allVideos.Count);

            foreach (var item in allVideos)
            {


                logger.LogWarning("item: {0}", item.Name);
                logger.LogWarning("path: {0}", item.Path);

                var videoStream = item.GetMediaStreams().Find(m => m.Type == MediaStreamType.Video);


                logger.LogWarning("videoStream: {0} Path: {1} Lang: {2}", videoStream, videoStream?.Path, videoStream?.Language);



                foreach (var s in item.GetMediaStreams())
                {

                    if (s.Type == MediaStreamType.Subtitle)
                    {
                        if (!s.IsInterlaced)
                        {
                            logger.LogWarning("subtitle stream needs to be syncd: {0} - {1}", s.Path, s.Language);

                            runSubSync(item, s);
                        }

                    }
                }
            }

            logger.LogWarning("Completed scheduled task");
            return Task.CompletedTask;
        }


        private void runSubSync(BaseItem item, MediaStream stream)
        {

            var config = Plugin.Instance!.Configuration;

            using (var pProcess = new System.Diagnostics.Process())
            {
                pProcess.StartInfo.FileName = config.SubSyncPath + "\\subsync.exe";
                var args = "--cli sync --sub \"" + stream.Path + "\" --ref \"" + item.Path + "\" --sub-lang " + stream.Language + " --out \"" + stream.Path + ".syncd\"";
                logger.LogWarning("args: {0}", args);
                pProcess.StartInfo.Arguments = args;

                pProcess.StartInfo.UseShellExecute = false;
                pProcess.StartInfo.RedirectStandardOutput = true;
                pProcess.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                pProcess.StartInfo.CreateNoWindow = true; //not diplay a windows
                /* pProcess.Start();
                 string output = pProcess.StandardOutput.ReadToEnd(); //The output result
                 pProcess.WaitForExit();
                 logger.LogInformation("output: {0}", output);*/
            }
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
           {
                // Every so often
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerStartup
                }
            };
        }
    }
}
