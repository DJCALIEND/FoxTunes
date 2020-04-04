﻿using FoxTunes.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FoxTunes
{
    public class PlaybackStatisticsBehaviour : StandardBehaviour, IDisposable
    {
        public PlaybackStatisticsBehaviour()
        {
            this.Queue = new Queue<PlaylistItem>();
        }

        public Queue<PlaylistItem> Queue { get; private set; }

        public IPlaylistManager PlaylistManager { get; private set; }

        public IPlaybackManager PlaybackManager { get; private set; }

        public IOutput Output { get; private set; }

        public IOutputStreamQueue OutputStreamQueue { get; private set; }

        public IConfiguration Configuration { get; private set; }

        public SelectionConfigurationElement Write { get; private set; }

        public override void InitializeComponent(ICore core)
        {
            this.PlaylistManager = core.Managers.Playlist;
            this.PlaybackManager = core.Managers.Playback;
            this.Output = core.Components.Output;
            this.OutputStreamQueue = core.Components.OutputStreamQueue;
            this.Configuration = core.Components.Configuration;
            this.Write = this.Configuration.GetElement<SelectionConfigurationElement>(
                MetaDataBehaviourConfiguration.SECTION,
                MetaDataBehaviourConfiguration.WRITE_ELEMENT
            );
            this.Configuration.GetElement<BooleanConfigurationElement>(
                MetaDataBehaviourConfiguration.SECTION,
                MetaDataBehaviourConfiguration.READ_POPULARIMETER_TAGS
            ).ConnectValue(value =>
            {
                if (value)
                {
                    this.Enable();
                }
                else
                {
                    this.Disable();
                }
            });
            base.InitializeComponent(core);
        }

        public void Enable()
        {
            if (this.Output != null)
            {
                this.Output.IsStartedChanged += this.OnIsStartedChanged;
            }
            if (this.PlaybackManager != null)
            {
                this.PlaybackManager.CurrentStreamChanged += this.OnCurrentStreamChanged;
            }
        }

        public void Disable()
        {
            if (this.Output != null)
            {
                this.Output.IsStartedChanged -= this.OnIsStartedChanged;
            }
            if (this.PlaybackManager != null)
            {
                this.PlaybackManager.CurrentStreamChanged -= this.OnCurrentStreamChanged;
            }
        }

        protected virtual void OnIsStartedChanged(object sender, AsyncEventArgs e)
        {
            if (this.Output.IsStarted)
            {
                return;
            }
            //When the output is stopped we can empty the queue.
#if NET40
            var task = TaskEx.Run(() => this.Dequeue());
#else
            var task = Task.Run(() => this.Dequeue());
#endif
        }

        protected virtual void OnCurrentStreamChanged(object sender, AsyncEventArgs e)
        {
            //Critical: Don't block in this event handler, it causes a deadlock.
#if NET40
            var task = TaskEx.Run(() => this.Enqueue(this.PlaybackManager.CurrentStream));
#else
            var task = Task.Run(() => this.Enqueue(this.PlaybackManager.CurrentStream));
#endif
        }

        protected virtual Task Enqueue(IOutputStream currentStream)
        {
            if (currentStream != null)
            {
                this.Queue.Enqueue(currentStream.PlaylistItem);
                if (this.Queue.Count > 1)
                {
                    //Only try to write if there's more than one item in the queue.
                    //Otherwise nothing can be written (as the track is being played).
                    return this.Dequeue();
                }
            }
#if NET40
            return TaskEx.FromResult(false);
#else
            return Task.CompletedTask;
#endif
        }

        protected virtual async Task Dequeue()
        {
            var count = this.Queue.Count;
            while (count-- > 0)
            {
                var playlistItem = this.Queue.Dequeue();
                if (this.Output.IsStarted)
                {
                    if (this.OutputStreamQueue.IsQueued(playlistItem))
                    {
                        //File is likely in use, enqueued. Push it to the back.
                        this.Queue.Enqueue(playlistItem);
                        continue;
                    }
                    if (this.PlaybackManager.CurrentStream != null && string.Equals(this.PlaybackManager.CurrentStream.FileName, playlistItem.FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        //File is likely in use, being played. Push it to the back.
                        this.Queue.Enqueue(playlistItem);
                        continue;
                    }
                }
                try
                {
                    await this.PlaylistManager.IncrementPlayCount(playlistItem).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.Write(this, LogLevel.Error, "Failed to update play count for file \"{0}\": {1}", playlistItem.FileName, e.Message);
                }
            }
        }


        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.IsDisposed || !disposing)
            {
                return;
            }
            this.OnDisposing();
            this.IsDisposed = true;
        }

        protected virtual void OnDisposing()
        {
            this.Disable();
        }

        ~PlaybackStatisticsBehaviour()
        {
            Logger.Write(this, LogLevel.Error, "Component was not disposed: {0}", this.GetType().Name);
            try
            {
                this.Dispose(true);
            }
            catch
            {
                //Nothing can be done, never throw on GC thread.
            }
        }
    }
}
