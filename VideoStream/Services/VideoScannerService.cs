using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.FileSystemGlobbing;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VideoStream.Data;
using VideoStream.Data.Medias;
using static System.Formats.Asn1.AsnWriter;

namespace VideoStream.Services
{
    public class VideoScannerService : IHostedService, IDisposable
    {
        private readonly ILogger<VideoScannerService> _logger;
        private Timer? _timer = null;
        private readonly IWebHostEnvironment _environment;
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly VideoPaths paths;
        private object _lock = new object();
        private List<FileSystemWatcher> watchers;
        private Queue<VideoInfo> thumbnailsGenQueue;
        private Queue<string> videoQueue;
        private List<string> videoPaths;

        public VideoScannerService(ILogger<VideoScannerService> logger, IWebHostEnvironment environment, IConfiguration configuration, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _environment = environment;
            this.serviceScopeFactory = serviceScopeFactory;
            this.paths = new VideoPaths();
            configuration.GetSection(VideoPaths.Config).Bind(paths);
            watchers = new List<FileSystemWatcher>();
            thumbnailsGenQueue = new Queue<VideoInfo>();
            videoQueue = new Queue<string>();

            videoPaths = new List<string>();

            videoPaths.AddRange(this.paths.Paths);
            videoPaths.Add(Path.Combine(_environment.WebRootPath, "video"));

            foreach (string path in videoPaths)
            {
                FileSystemWatcher watcher = new FileSystemWatcher(path);

                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;

                watcher.Filters.Add("*.mp4");
                watcher.Filters.Add("*.webm");
                watcher.Filters.Add("*.mkv");
                watcher.IncludeSubdirectories = true;
                watcher.Created += NewVideo;
                watcher.Deleted += VideoDeleted;
                watcher.EnableRaisingEvents = true;
                watchers.Add(watcher);
            }
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service running.");

            foreach (string path in videoPaths)
            {
                string[] files = Directory.GetFiles(path, "", SearchOption.AllDirectories);

                foreach (string file in files)
                {
                    videoQueue.Enqueue(file);
                }
            }
            RemoveVideoInfos();

            SetTime();

            return Task.CompletedTask;
        }

        private void VideoDeleted(object sender, FileSystemEventArgs e)
        {
            using (IServiceScope scope = serviceScopeFactory.CreateAsyncScope())
            {
                using (ApplicationDbContext? context = scope.ServiceProvider?.GetService<IDbContextFactory<ApplicationDbContext>>()?.CreateDbContext())
                {
                    if (context != null)
                    {
                        VideoPath? path = context.VideoPaths.Where(p => p.Path == e.FullPath).FirstOrDefault();

                        if (path != null)
                        {
                            _logger.LogInformation($"Deleting Video Information. - {e.Name}");

                            VideoInfo? info = context.VideoInfo.Find(path.VideoInfoId);

                            if (info != null)
                            {
                                if (info.Thumbnail != Path.Combine("img", "video.jpg"))
                                {
                                    if (info.Thumbnail != null)
                                    {
                                        File.Delete(Path.Combine(_environment.WebRootPath, info.Thumbnail));
                                    }
                                }

                                context.VideoInfo.Remove(info);
                                context.SaveChanges();
                            }
                        }
                    }
                }
            }
        }

        private void NewVideo(object sender, FileSystemEventArgs e)
        {
            videoQueue.Enqueue(e.FullPath);
        }

        private void SetTime()
        {
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        }

        private void DoWork(object? state)
        {
            lock (_lock)
            {
                CreateVideoInfos();
                CheckVideoInfoThumbnails();
            }
        }

        private void CreateVideoInfos()
        {
            using (IServiceScope scope = serviceScopeFactory.CreateAsyncScope())
            {
                IDbContextFactory<ApplicationDbContext>? contextFactory = scope.ServiceProvider?.GetService<IDbContextFactory<ApplicationDbContext>>();
                if(contextFactory != null) 
                {
                    using (ApplicationDbContext? context = contextFactory.CreateDbContext())
                    {
                        while (videoQueue.Count > 0)
                        {
                            if (IsFileLocked(videoQueue.Peek()))
                                continue;

                            string file = videoQueue.Dequeue();

                            CreateVideoInfo(file, context);
                        }
                    }
                }
            }
        }


        private void RemoveVideoInfos()
        {
            using (IServiceScope scope = serviceScopeFactory.CreateAsyncScope())
            {
                using (ApplicationDbContext? context = scope.ServiceProvider?.GetService<IDbContextFactory<ApplicationDbContext>>()?.CreateDbContext())
                {
                    if (context != null)
                    {
                        foreach (VideoInfo info in context.VideoInfo)
                        {
                            context.VideoInfo.Entry(info).Reference(b => b.Path).Load();

                            if (info.Path != null)
                            {
                                if (!File.Exists(info.Path.Path))
                                {
                                    _logger.LogInformation($"Deleting Video Information. - {info.Name}");

                                    if (info.Thumbnail != Path.Combine("img", "video.jpg"))
                                    {
                                        File.Delete(Path.Combine(_environment.WebRootPath, info.Thumbnail));
                                    }

                                    context.VideoInfo.Remove(info);
                                }
                            }
                        }

                        context.SaveChanges();
                    }
                }
            }
            
        }
        
        private void CheckVideoInfoThumbnails()
        {
            while (thumbnailsGenQueue.Count > 0)
            {
                VideoInfo info = thumbnailsGenQueue.Dequeue();

                if (info.Path != null)
                {
                    string thumbnailPath = Path.Combine(_environment.WebRootPath, info.Thumbnail);

                    if (!File.Exists(thumbnailPath) || string.IsNullOrEmpty(info.Thumbnail))
                    {
                        try
                        {
                            if (info.Path != null)
                            {
                                if (info.Path.Path != null)
                                {
                                    if (!FFMpeg.Snapshot(info.Path.Path, thumbnailPath, null, TimeSpan.FromSeconds(1)))
                                    {
                                        _logger.LogInformation("Creating Thumbnail.");
                                    }
                                }
                            }
                        }
                        catch
                        {
                            _logger.LogError("Error creating Thumbnail.");

                            info.Thumbnail = Path.Combine("img", "video.jpg");
                        }
                    }
                }
            }
        }

        private void CreateVideoInfo(string file, ApplicationDbContext context)
        {
            string[] allowedExt = { ".webm", ".mkv", ".mp4" };

            string fileExt = Path.GetExtension(file);

            if (!allowedExt.Contains(fileExt))
            {
                return;
            }

            string hash = CalculateMD5(file);

            VideoPath? videoPath = context.VideoPaths.Where(p => p.SHA == hash && p.Path == file).FirstOrDefault();

            if (videoPath != null)
            {
                VideoInfo? videoInfo = context.VideoInfo.Find(videoPath.VideoInfoId);

                if (videoInfo != null)
                {
                    if (!File.Exists(Path.Combine(_environment.WebRootPath, videoInfo.Thumbnail)))
                    {
                        if (videoInfo.Name != null)
                        {
                            videoInfo.Thumbnail = Path.Combine("img", "thumbnail", $"{videoInfo.Name}.png");
                        }
                        context.VideoInfo.Update(videoInfo);
                        context.SaveChanges();
                    }
                }
                else
                {
                    videoInfo = CreateVideoInfo(file);

                    videoInfo.Path = videoPath;

                    context.VideoInfo.Add(videoInfo);
                    thumbnailsGenQueue.Enqueue(videoInfo);
                    context.SaveChanges();
                }
            }
            else
            {
                VideoInfo? videoInfo = CreateVideoInfo(file);

                videoPath = new VideoPath()
                {
                    Id = Guid.NewGuid(),
                    VideoInfoId = videoInfo.Id,
                    SHA = hash,
                    Path = file
                };

                videoInfo.Path = videoPath;

                context.VideoInfo.Add(videoInfo);
                thumbnailsGenQueue.Enqueue(videoInfo);
                context.SaveChanges();
            }
        }

        private VideoInfo CreateVideoInfo(string file)
        {
            string fileName = Path.GetFileName(file);

            VideoInfo info = new VideoInfo();
            info.Id = Guid.NewGuid();
            info.Name = Path.GetFileNameWithoutExtension(fileName);
            info.FullName = fileName;
            info.Thumbnail = Path.Combine("img", "thumbnail", $"{info.Name}.png");
            info.MediaType = Path.GetExtension(file).Remove(0, 1);

            _logger.LogInformation($"Adding Video. - {info.Name}");

            switch (info.MediaType)
            {
                case "mkv":
                    info.MediaType = "mp4";
                    break;
            }

            return info;
        }


        private string CalculateMD5(string filename)
        {
            byte[] hash;
            using (MD5 md5 = MD5.Create())
            {
                using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    hash = md5.ComputeHash(stream);
                }
            }

            return Encoding.UTF8.GetString(hash);
        }

        private bool IsFileLocked(string file)
        {
            try
            {
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }

            //file is not locked
            return false;
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
