using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Threading.Tasks.Dataflow;
using SkiaSharp;
using System.Diagnostics;
using static SkiaSharp.SKBitmapResizeMethod;

namespace DownloadPicSum
{
    public class PicSumDequeue
    {
        private bool _isStop;
        Task _taskDownloadImg;
        Task _taskResizeImg;
        Task _taskSaveImg;
        DequeueWorkerConfig _config;
        SizeImgConfig _imgResizeConfig;
        CancellationTokenSource _cancel;
        private const string _queueNameResize = "ResizeIMG";
        ConcurrentQueue<QueueResizeImg> _queueSplitedResizeImg = new ConcurrentQueue<QueueResizeImg>();
        ConcurrentQueue<SKData> _queueSplitedSaveImg = new ConcurrentQueue<SKData>();
        public PicSumDequeue()
        {
            _isStop = false;
            _config = new DequeueWorkerConfig { BatchSize = 100 };
            //_imgResizeConfig = new SizeImgConfig { Width = 500, Height = 500, PathSave = "C:\\Users\\daoha\\OneDrive\\Desktop\\DownloadPicSum\\DownloadPicSum\\Img\\" };
            _imgResizeConfig = new SizeImgConfig { Width = 200, Height = 300, PathSave = "C:\\Users\\daoha\\OneDrive\\Desktop\\DownloadPicSum\\DownloadPicSum\\Img\\" };
        }
        public async Task Start()
        {
            //if (!_isStop)
            //{
            //    _cancel = new CancellationTokenSource();
            //}
            //CancellationToken cancellationToken = _cancel.Token;

            _taskDownloadImg = HandleDownload();
            _taskResizeImg = ResizeIMG();
            _taskSaveImg = HandleSaveFile();
        }

        private Task<Task> HandleDownload()
        {
            return Task.FromResult(Task.Run(async () =>
            {
                var interval = _config.IntervalMiliseconds / 3;
                if (interval == 0) interval = 100;

                List<Task<byte[]>> items = new List<Task<byte[]>>();
                while (!_isStop)
                {
                    try
                    {
                        //cancellationToken.ThrowIfCancellationRequested();

                        for (var i = 0; i < _config.BatchSize; i++)
                        {
                            using var client = new WebClient();
                            var url = $"https://picsum.photos/{_imgResizeConfig.Width}/{_imgResizeConfig.Height}";

                            items.Add(client.DownloadDataTaskAsync(url));
                            Console.WriteLine($"Add Img {i + 1} Success");
                        }

                        if (items.Count == 0) continue;

                        var lstTaskByte = await Task.WhenAll(items);

                        foreach (var i in lstTaskByte)
                        {
                            _queueSplitedResizeImg.Enqueue(new QueueResizeImg()
                            {
                                QueueName = _queueNameResize,
                                Data = i,
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                    finally
                    {
                        await Task.Delay(interval);
                    }
                }
            }));
        }

        private async Task<Task> ResizeIMG()
        {
            return Task.Run(async () =>
            {
                var datas = new List<QueueResizeImg>();
                while (!_isStop)
                {
                    try
                    {
                        for (var i = 0; i < _config.BatchSize; i++)
                        {
                            if (_queueSplitedResizeImg.TryDequeue(out QueueResizeImg itm))
                            {
                                datas.Add(itm);
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (datas.Count == 0) continue;
                        // if (datas.Count != _config.BatchSize) continue;

                        //var result = HandleResizeImg(datas);

                        //Console.WriteLine(result.IsCompleted
                        //    ? $"**** All task HandleResizeImg start and finish: {result.IsCompleted}"
                        //    : $"**** Waiting... HandleResizeImg");

                        HandleResizeImgUsingActionBlock(datas);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{DateTime.Now} ERROR , {ex}");

                    }
                    finally
                    {
                        await Task.Delay(_config.IntervalMiliseconds);
                    }
                }
            });
        }

        private async Task<ParallelLoopResult> HandleResizeImg(List<QueueResizeImg> datas)
        {
            //foreach (var item in datas)
            //{
            //    var original = SkiaSharp.SKBitmap.Decode(item.Data).Resize(new SKImageInfo(_imgResizeConfig.ResizedWidth, _imgResizeConfig.ResizedHeight), SKBitmapResizeMethod.Lanczos3); ;

            //    var image = SKImage.FromBitmap(original).Encode(SKEncodedImageFormat.Png, 90);

            //    _queueSplitedSaveImg.Enqueue(image);
            //}

            //Parallel
            var timer = new Stopwatch();
            timer.Start();
            Console.WriteLine($"***** Watch Start HandleResizeImg: {timer.Elapsed:m\\:ss\\.fff}");

            var result = await Task.Run(() => Parallel.ForEach(
                                                    datas,
                                                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                                                    s =>
                                                    {
                                                        var original = SkiaSharp.SKBitmap.Decode(s.Data)
                                                            .Resize(
                                                                new SKImageInfo(_imgResizeConfig.ResizedWidth,
                                                                    _imgResizeConfig.ResizedHeight),
                                                                Lanczos3);

                                                        var image = SKImage.FromBitmap(original).Encode(SKEncodedImageFormat.Png, 90);

                                                        _queueSplitedSaveImg.Enqueue(image);
                                                    }));

            Console.WriteLine($"***** Watch End HandleResizeImg: {timer.Elapsed:m\\:ss\\.fff}");
            timer.Stop();
            return result;
        }
             private async Task HandleResizeImgUsingActionBlock(List<QueueResizeImg> datas)
        {
            //foreach (var item in datas)
            //{
            //    var original = SkiaSharp.SKBitmap.Decode(item.Data).Resize(new SKImageInfo(_imgResizeConfig.ResizedWidth, _imgResizeConfig.ResizedHeight), SKBitmapResizeMethod.Lanczos3); ;

            //    var image = SKImage.FromBitmap(original).Encode(SKEncodedImageFormat.Png, 90);

            //    _queueSplitedSaveImg.Enqueue(image);
            //}

            //Parallel
            var timer = new Stopwatch();
            timer.Start();
            Console.WriteLine($"***** Watch Start HandleResizeImg: {timer.Elapsed:m\\:ss\\.fff}");

            ActionBlock<QueueResizeImg> actionBlock = new ActionBlock<QueueResizeImg>(async (data) =>
            {
                var original = SkiaSharp.SKBitmap.Decode(data.Data)
                                                            .Resize(
                                                                new SKImageInfo(_imgResizeConfig.ResizedWidth,
                                                                    _imgResizeConfig.ResizedHeight),
                                                                Lanczos3);

                var image = SKImage.FromBitmap(original).Encode(SKEncodedImageFormat.Png, 90);

                _queueSplitedSaveImg.Enqueue(image);

                Console.WriteLine("ThreaId => " + Thread.CurrentThread.ManagedThreadId);

            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount });

            foreach (var x in datas)
            {
                await actionBlock.SendAsync(x);
            }
            actionBlock.Complete();
            await actionBlock.Completion;

            Console.WriteLine($"***** Watch End HandleResizeImg: {timer.Elapsed:m\\:ss\\.fff}");
            timer.Stop();
        }

        private Task<Task> HandleSaveFile()
        {
            return Task.FromResult<Task>(Task.FromResult(Task.Run( async () =>
            {
                while (!_isStop)
                {
                    var lstSkData = new List<SKData>();

                    var i = 0;
                    for (; i < _config.BatchSize; i++)
                    {
                        var item = _queueSplitedSaveImg.TryDequeue(out var data);

                        if (!item) break;
                        else lstSkData.Add(data);
                    }

                    if (lstSkData.Count >= 10) continue;

                    // Parallel.ForEach(
                    //     lstSkData,
                    // new ParallelOptions { MaxDegreeOfParallelism = 10 },
                    // s =>
                    // {
                    //     var pathSave = _imgResizeConfig.PathSave + Guid.NewGuid() + ".png";
                    //     using var stream = new FileStream(pathSave, FileMode.Create, FileAccess.Write);
                    //     s.SaveTo(stream);
                    // });

                    ActionBlock<SKData> actionBlock = new ActionBlock<SKData>(async (input) =>
                    {
                        var pathSave = _imgResizeConfig.PathSave + Guid.NewGuid() + ".png";
                        await using var stream = new FileStream(pathSave, FileMode.Create, FileAccess.Write);
                        input.SaveTo(stream);

                    }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount });

                    foreach (var x in lstSkData)
                    {
                        await actionBlock.SendAsync(x);
                    }
                    actionBlock.Complete();
                    await actionBlock.Completion;

                    //foreach (var item in lstSkData)
                    //{
                    //    var pathSave = _imgResizeConfig.PathSave + Guid.NewGuid() + ".png";
                    //    using var stream = new FileStream(pathSave, FileMode.Create, FileAccess.Write);
                    //    item.SaveTo(stream);
                    //}
                }
            })));
        }
    }

    public class DequeueWorkerConfig
    {
        public int IntervalMiliseconds { get; set; } = 1000;
        public int BatchSize { get; set; } = 200;
    }

    public class SizeImgConfig
    {
        public int Width { get; set; } = 200;
        public int Height { get; set; } = 300;
        public int ResizedWidth { get; set; } = 100;
        public int ResizedHeight { get; set; } = 150;
        public string PathSave { get; set; }
    }

    public class QueueResizeImg
    {
        public byte[] Data { get; set; }
        public string QueueName { get; set; }
    }
}
