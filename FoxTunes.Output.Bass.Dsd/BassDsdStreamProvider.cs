﻿using FoxTunes.Interfaces;
using ManagedBass;
using ManagedBass.Dsd;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FoxTunes
{
    //This component does not technically require an output but we don't want to present anything else with DSD data.
    [ComponentDependency(Slot = ComponentSlots.Output)]
    public class BassDsdStreamProvider : BassStreamProvider
    {
        public IBassStreamPipelineFactory BassStreamPipelineFactory { get; private set; }

        public override byte Priority
        {
            get
            {
                return PRIORITY_HIGH;
            }
        }

        public override void InitializeComponent(ICore core)
        {
            this.BassStreamPipelineFactory = ComponentRegistry.Instance.GetComponent<IBassStreamPipelineFactory>();
            base.InitializeComponent(core);
        }

        public override bool CanCreateStream(PlaylistItem playlistItem)
        {
            if (!new[]
            {
                "dsd",
                "dsf"
            }.Contains(playlistItem.FileName.GetExtension(), StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
            var query = this.BassStreamPipelineFactory.QueryPipeline();
            if ((query.OutputCapabilities & BassCapability.DSD_RAW) != BassCapability.DSD_RAW)
            {
                return false;
            }
            return true;
        }

        public override Task<IBassStream> CreateStream(PlaylistItem playlistItem, IEnumerable<IBassStreamAdvice> advice)
        {
            var flags = BassFlags.Decode | BassFlags.DSDRaw;
            return this.CreateStream(playlistItem, flags, advice);
        }

#if NET40
        public override Task<IBassStream> CreateStream(PlaylistItem playlistItem, BassFlags flags, IEnumerable<IBassStreamAdvice> advice)
#else
        public override async Task<IBassStream> CreateStream(PlaylistItem playlistItem, BassFlags flags, IEnumerable<IBassStreamAdvice> advice)
#endif
        {
            if (!flags.HasFlag(BassFlags.DSDRaw))
            {
#if NET40
                return base.CreateStream(playlistItem, flags, advice);
#else
                return await base.CreateStream(playlistItem, flags, advice).ConfigureAwait(false);
#endif
            }
#if NET40
            this.Semaphore.Wait();
#else
            await this.Semaphore.WaitAsync().ConfigureAwait(false);
#endif
            try
            {
                var channelHandle = default(int);
                if (this.Output != null && this.Output.PlayFromMemory)
                {
                    channelHandle = BassDsdInMemoryHandler.CreateStream(playlistItem.FileName, 0, 0, flags);
                    if (channelHandle == 0)
                    {
                        Logger.Write(this, LogLevel.Warn, "Failed to load file into memory: {0}", playlistItem.FileName);
                        channelHandle = BassDsd.CreateStream(playlistItem.FileName, 0, 0, flags);
                    }
                }
                else
                {
                    channelHandle = BassDsd.CreateStream(playlistItem.FileName, 0, 0, flags);
                }
                if (channelHandle != 0)
                {
                    var query = this.BassStreamPipelineFactory.QueryPipeline();
                    var channels = BassUtils.GetChannelCount(channelHandle);
                    var rate = BassUtils.GetChannelDsdRate(channelHandle);
                    if (query.OutputChannels < channels || !query.OutputRates.Contains(rate))
                    {
                        Logger.Write(this, LogLevel.Warn, "DSD format {0}:{1} is unsupported, the stream will be unloaded. This warning is expensive, please don't attempt to play unsupported DSD.", rate, channels);
                        this.FreeStream(playlistItem, channelHandle);
                        channelHandle = 0;
                    }
                }
#if NET40
                return TaskEx.FromResult(this.CreateStream(channelHandle, advice));
#else
                return this.CreateStream(channelHandle, advice);
#endif
            }
            finally
            {
                this.Semaphore.Release();
            }
        }
    }
}
