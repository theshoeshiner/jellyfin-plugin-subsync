using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Plugin.Subsync.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubSync
{
    internal class ServerEntryPoint : IServerEntryPoint
    {

        private IServerConfigurationManager? _configurationManager;
        private IMetadataSaver? _metadataSaver;
        private ILogger<ServerEntryPoint> _logger;
        private ILibraryManager manager;
        private ISubtitleManager _subtitleManager;
        private ITaskManager taskManager;
        public ServerEntryPoint(ILogger<ServerEntryPoint> logger, ILibraryManager libManager, ISubtitleManager subtitleManager, ITaskManager taskManager)
        {
            _logger = logger;
            _logger.LogWarning("ServerEntryPoint: {0}", libManager);
            manager = libManager;
            _subtitleManager = subtitleManager;
            this.taskManager = taskManager;
            manager.ItemAdded += OnItemAdded;
            manager.ItemUpdated += OnItemUpdated;
            manager.ItemRemoved += OnItemRemoved;
        }

        private void OnItemAdded(object? sender, ItemChangeEventArgs e)
        {

            _logger.LogWarning("OnItemAdded sender: {0} item: {1}", sender, e.Item);
            var item = e.Item;
            checkStreams(e.Item);

        }

        private void OnItemUpdated(object? sender, ItemChangeEventArgs e)
        {

            _logger.LogWarning("OnItemUpdated sender: {0} item: {1}", sender, e.Item);
            checkStreams(e.Item);
        }

        private void OnItemRemoved(object? sender, ItemChangeEventArgs e)
        {

            _logger.LogWarning("OnItemRemoved sender: {0} item: {1}", sender, e.Item);
        }

        private void checkStreams(BaseItem item)
        {
            _logger.LogInformation("checkStreams: {0}", item.GetType());
            if (item.GetType() == typeof(Movie))
            {
                //_logger.LogInformation("was movie");
                var movie = (Movie)item;


                //MediaBrowser.Controller.Entities.Movies.Movie movie = (MediaBrowser.Controller.Entities.Movies.Movie)item;

                //_logger.LogInformation("checkStreams: {0}", movie.GetMediaStreams());

                foreach (var s in movie.GetMediaStreams())
                {
                    // _logger.LogWarning("stream type: {0} = {1}", s.Type, s);

                    if (s.Type == MediaStreamType.Subtitle)
                    {
                        if (s.IsInterlaced)
                        {

                        }
                        else
                        {
                            _logger.LogWarning("subtitle stream needs to be syncd: {0}", s);
                            var pc = Plugin.Instance!.Configuration;

                            taskManager.QueueIfNotRunning<ScheduledTask>();

                            _logger.LogWarning("queued task");

                        }
                    }
                }

            }
        }
        public void Dispose()
        {
            Console.Out.WriteLine("Dispose");

        }

        public Task RunAsync()
        {
            Console.Out.WriteLine("RunAsync");

            return Task.CompletedTask;
        }
    }
}
