using ten.bew.Caching;
using i.ten.bew;
using i.ten.bew.Messaging;
using i.ten.bew.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

namespace ten.bew.Server.Chunks
{
    internal class ParallelChunk : ChunkBase
    {
        private List<ChunkBase> _chunks;
        private Task _initTask;
        private bool _initialised;

        public class Creator : ChunkCreator
        {
            protected internal override ChunkBase Create(IServer server, dynamic parameters)
            {
                var serializedParameter = JsonConvert.SerializeObject(parameters);
                return new ParallelChunk(server, serializedParameter);
            }
        }

        public ParallelChunk(IServer server, string serializedParameter)
            : base(server)
        {
            _initTask = Task.Factory.StartNew(() =>
            {
                PageChunk.PageDescription pageDescription = JsonConvert.DeserializeObject<PageChunk.PageDescription>(serializedParameter);
                _chunks = new List<ChunkBase>();
                PageChunk.BuildChunks(server, _chunks, pageDescription);
                _initialised = true;
            });
        }

        public override async Task Send(HttpClientImpl client, Stream redirectionStream)
        {
            if (_initialised == false)
            {
                await _initTask;
            }

            if (redirectionStream == null)
            {
                redirectionStream = client.Context.Response.OutputStream;
            }

            try
            {
                List<Task> sendTasks = new List<Task>();
                List<Task> continueTasks = new List<Task>();
                CancellationTokenSource source = new CancellationTokenSource();

                try
                {
                    for (int i = 0; i < _chunks.Count; i++)
                    {
                        var chunk = _chunks[i];

                        MemoryStream outputTo = new MemoryStream();
                        Task sendTask = chunk.Send(client, outputTo);

                        if(sendTask.Status == TaskStatus.Faulted)
                        {
                            Debug.Assert(chunk.GetType().Name.Contains("CsvChunk"));
                        }

                        sendTasks.Add(sendTask);

                        var continueTask = sendTask.ContinueWith(
                                                    (previousTask) =>
                                                    {
                                                        try
                                                        {
                                                            if (source.IsCancellationRequested == false)
                                                            {
                                                                using (outputTo)
                                                                {
                                                                    if (previousTask.Status == TaskStatus.RanToCompletion)
                                                                    {
                                                                        outputTo.Position = 0;

                                                                        lock (redirectionStream)
                                                                        {
                                                                            outputTo.CopyTo(redirectionStream);
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                Debug.WriteLine("Cancelled");
                                                            }
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            source.Cancel(true);
                                                        }
                                                    },
                                                    source.Token,
                                                    TaskContinuationOptions.None,
                                                    TaskScheduler.Current
                                                );

                        continueTasks.Add(continueTask);
                    }

                    foreach(var task in sendTasks)
                    {
                        await task;                    
                    }

                    foreach (var task in continueTasks)
                    {
                        await task;                    
                    }

                    TracingChunksSource.TraceEvent(TraceEventType.Verbose, TRACEEVENT_OK, "ParallelChunk");

                    //bool allCompleted = Task.WaitAll(tasks.ToArray(), 60000, source.Token);

                    //if(allCompleted == false)
                    //{
                    //  TracingChunksSource.TraceEvent(TraceEventType.Warning, TRACEEVENT_TIMEOUT, client.Context.Request.Url);
                    //}
                }
                catch(AggregateException aex)
                {      
                    source.Cancel(true);

                    foreach(var ex in aex.InnerExceptions)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
                catch(OperationCanceledException opcex)
                {
                    Debug.WriteLine(opcex.Message);
                }
                catch(Exception ex)
                {
                    source.Cancel(true);
                    Debug.WriteLine(ex.Message);
                }
            }
            finally
            {
            }
        }
    }
}
