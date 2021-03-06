﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using osu_StreamCompanion.Code.Misc;
using StreamCompanionTypes;
using StreamCompanionTypes.DataTypes;
using StreamCompanionTypes.Interfaces;

namespace osu_StreamCompanion.Code.Core.Maps.Processing
{
    public class MapStringFormatter : IModule, ISettings, IDisposable
    {
        private readonly SettingNames _names = SettingNames.Instance;
        private ILogger _logger;
        private readonly MainMapDataGetter _mainMapDataGetter;
        private readonly List<IOsuEventSource> _osuEventSources;
        private ISettingsHandler _settings;
        private string _lastMsnString = "";
        private Thread ConsumerThread;
        private ConcurrentStack<MapSearchArgs> TasksMsn = new ConcurrentStack<MapSearchArgs>();
        private ConcurrentStack<MapSearchArgs> TasksMemory = new ConcurrentStack<MapSearchArgs>();

        public MapStringFormatter(MainMapDataGetter mainMapDataGetter, List<IOsuEventSource> osuEventSources)
        {
            _mainMapDataGetter = mainMapDataGetter;
            _osuEventSources = osuEventSources;
            foreach (var source in osuEventSources)
            {
                source.NewOsuEvent += NewOsuEvent;
            }
            ConsumerThread = new Thread(ConsumerTask);
        }

        private void NewOsuEvent(object sender, MapSearchArgs mapSearchArgs)
        {
            if (mapSearchArgs == null)
                return;
            //TODO: priority system for IOsuEventSource 
            if (mapSearchArgs.SourceName == "OsuMemory")
            {
                TasksMemory.Clear();
                TasksMemory.Push(mapSearchArgs);
            }
            else
            {
                TasksMsn.Clear();
                TasksMsn.Push(mapSearchArgs);
            }

        }

        public bool Started { get; set; }
        public void Start(ILogger logger)
        {
            _logger = logger;
            ConsumerThread.Start();
        }
        public void SetSettingsHandle(ISettingsHandler settings)
        {
            _settings = settings;
        }


        public void ConsumerTask()
        {
            try
            {
                bool isPoolingEnabled = _settings.Get<bool>(_names.EnableMemoryPooling);
                int counter = 0;
                MapSearchArgs searchArgs;
                MapSearchResult searchResult;
                var memorySearchFailed = false;
                while (true)
                {
                    if (counter % 400 == 0)
                    {//more or less every 2 seconds given 5ms delay at end.
                        counter = 0;
                        isPoolingEnabled = _settings.Get<bool>(_names.EnableMemoryPooling);
                    }
                    counter++;
                    if (isPoolingEnabled)
                    {
                        //Here we prioritize Memory events over MSN/other.
                        if (TasksMemory.TryPop(out searchArgs))
                        {
                            if (searchArgs.MapId == 0 && string.IsNullOrEmpty(searchArgs.MapHash))
                            {
                                memorySearchFailed = true;
                            }
                            else
                            {
                                searchResult = _mainMapDataGetter.FindMapData(searchArgs);
                                if (searchResult.FoundBeatmaps)
                                {
                                    memorySearchFailed = false;
                                    searchResult.EventSource = searchArgs.SourceName;
                                    _mainMapDataGetter.ProcessMapResult(searchResult);
                                }
                                else
                                    memorySearchFailed = true;
                            }
                        }
                        if (memorySearchFailed)
                        {
                            if (TasksMsn.TryPop(out searchArgs))
                            {
                                searchResult = _mainMapDataGetter.FindMapData(searchArgs);
                                searchResult.EventSource = searchArgs.SourceName;
                                _mainMapDataGetter.ProcessMapResult(searchResult);
                            }
                        }
                    }
                    else
                    {
                        //Use MSN/other events only
                        if (TasksMsn.TryPop(out searchArgs))
                        {
                            searchResult = _mainMapDataGetter.FindMapData(searchArgs);
                            searchResult.EventSource = searchArgs.SourceName;
                            _mainMapDataGetter.ProcessMapResult(searchResult);
                        }
                    }
                    Thread.Sleep(5);
                }
            }
            catch (ThreadAbortException ex)
            {
                //Console.WriteLine("Consumer thread aborted");
            }
            finally
            {
            }
        }


        public void Dispose()
        {
            ConsumerThread?.Abort();
        }
    }
}
