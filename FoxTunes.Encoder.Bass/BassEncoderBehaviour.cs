﻿using FoxTunes.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FoxTunes
{
    [ComponentDependency(Slot = ComponentSlots.UserInterface)]
    public class BassEncoderBehaviour : StandardBehaviour, IBackgroundTaskSource, IReportSource, IInvocableComponent, IConfigurableComponent
    {
        public const string ENCODE = "GGGG";

        public ICore Core { get; private set; }

        public ILibraryManager LibraryManager { get; private set; }

        public IPlaylistManager PlaylistManager { get; private set; }

        public ILibraryHierarchyBrowser LibraryHierarchyBrowser { get; private set; }

        public IConfiguration Configuration { get; private set; }

        public IEnumerable<string> Profiles { get; private set; }

        public BooleanConfigurationElement Enabled { get; private set; }

        public BooleanConfigurationElement CopyTags { get; private set; }

        public override void InitializeComponent(ICore core)
        {
            this.Core = core;
            this.LibraryManager = core.Managers.Library;
            this.PlaylistManager = core.Managers.Playlist;
            this.LibraryHierarchyBrowser = core.Components.LibraryHierarchyBrowser;
            this.Profiles = ComponentRegistry.Instance.GetComponents<IBassEncoderSettings>().Select(
                settings => settings.Name
            ).ToArray();
            this.Configuration = core.Components.Configuration;
            this.Enabled = this.Configuration.GetElement<BooleanConfigurationElement>(
                BassEncoderBehaviourConfiguration.SECTION,
                BassEncoderBehaviourConfiguration.ENABLED_ELEMENT
            );
            this.CopyTags = this.Configuration.GetElement<BooleanConfigurationElement>(
                BassEncoderBehaviourConfiguration.SECTION,
                BassEncoderBehaviourConfiguration.COPY_TAGS
            );
            base.InitializeComponent(core);
        }

        public IEnumerable<IInvocationComponent> Invocations
        {
            get
            {
                if (this.Enabled.Value)
                {
                    if (this.LibraryManager.SelectedItem != null)
                    {
                        foreach (var profile in this.Profiles)
                        {
                            yield return new InvocationComponent(InvocationComponent.CATEGORY_LIBRARY, ENCODE, profile, path: "Convert");
                        }
                    }
                    if (this.PlaylistManager.SelectedItems != null && this.PlaylistManager.SelectedItems.Any())
                    {
                        foreach (var profile in this.Profiles)
                        {
                            yield return new InvocationComponent(InvocationComponent.CATEGORY_PLAYLIST, ENCODE, profile, path: "Convert");
                        }
                    }
                }
            }
        }

        public Task InvokeAsync(IInvocationComponent component)
        {
            switch (component.Id)
            {
                case ENCODE:
                    switch (component.Category)
                    {
                        case InvocationComponent.CATEGORY_LIBRARY:
                            return this.EncodeLibrary(component.Name);
                        case InvocationComponent.CATEGORY_PLAYLIST:
                            return this.EncodePlaylist(component.Name);
                    }
                    break;
            }
#if NET40
            return TaskEx.FromResult(false);
#else
            return Task.CompletedTask;
#endif
        }

        public IEnumerable<ConfigurationSection> GetConfigurationSections()
        {
            return BassEncoderBehaviourConfiguration.GetConfigurationSections();
        }

        public Task EncodeLibrary(string profile)
        {
            if (this.LibraryManager == null || this.LibraryManager.SelectedItem == null)
            {
#if NET40
                return TaskEx.FromResult(false);
#else
                return Task.CompletedTask;
#endif
            }
            //TODO: Warning: Buffering a potentially large sequence. It might be better to run the query multiple times.
            var libraryItems = this.LibraryHierarchyBrowser.GetItems(
                this.LibraryManager.SelectedItem,
                true
            ).ToArray();
            if (!libraryItems.Any())
            {
#if NET40
                return TaskEx.FromResult(false);
#else
                return Task.CompletedTask;
#endif
            }
            return this.Encode(libraryItems, profile);
        }

        public Task EncodePlaylist(string profile)
        {
            if (this.PlaylistManager.SelectedItems == null)
            {
#if NET40
                return TaskEx.FromResult(false);
#else
                return Task.CompletedTask;
#endif
            }
            var playlistItems = this.PlaylistManager.SelectedItems.ToArray();
            if (!playlistItems.Any())
            {
#if NET40
                return TaskEx.FromResult(false);
#else
                return Task.CompletedTask;
#endif
            }
            return this.Encode(playlistItems, profile);
        }

        public async Task Encode(IFileData[] fileDatas, string profile)
        {
            var factory = new EncoderItemFactory();
            factory.InitializeComponent(this.Core);
            var encoderItems = default(EncoderItem[]);
            try
            {
                encoderItems = factory.Create(fileDatas, profile);
            }
            catch (OperationCanceledException)
            {
                //Browse dialog was cancelled.
                return;
            }
            using (var task = new EncodeTask(this, fileDatas, encoderItems))
            {
                task.InitializeComponent(this.Core);
                this.OnBackgroundTask(task);
                await task.Run().ConfigureAwait(false);
                this.OnReport(task.EncoderItems);
            }
        }

        protected virtual void OnBackgroundTask(IBackgroundTask backgroundTask)
        {
            if (this.BackgroundTask == null)
            {
                return;
            }
            this.BackgroundTask(this, new BackgroundTaskEventArgs(backgroundTask));
        }

        public event BackgroundTaskEventHandler BackgroundTask;

        protected virtual void OnReport(EncoderItem[] encoderItems)
        {
            var report = new BassEncoderReport(encoderItems);
            report.InitializeComponent(this.Core);
            this.OnReport(report);
        }

        protected virtual void OnReport(IReport report)
        {
            if (this.Report == null)
            {
                return;
            }
            this.Report(this, new ReportEventArgs(report));
        }

        public event ReportEventHandler Report;

        private class EncodeTask : BackgroundTask
        {
            public const string ID = "0E86B088-F040-444C-859D-90AD426EF33C";

            public static readonly IBassEncoderFactory EncoderFactory = ComponentRegistry.Instance.GetComponent<IBassEncoderFactory>();

            private EncodeTask() : base(ID)
            {
                this.CancellationToken = new CancellationToken();
            }

            public EncodeTask(BassEncoderBehaviour behaviour, IFileData[] fileDatas, EncoderItem[] encoderItems) : this()
            {
                this.Behaviour = behaviour;
                this.FileDatas = fileDatas;
                this.EncoderItems = encoderItems;
            }

            public override bool Visible
            {
                get
                {
                    return true;
                }
            }

            public override bool Cancellable
            {
                get
                {
                    return true;
                }
            }

            public CancellationToken CancellationToken { get; private set; }

            public BassEncoderBehaviour Behaviour { get; private set; }

            public IFileData[] FileDatas { get; private set; }

            public EncoderItem[] EncoderItems { get; private set; }

            public ICore Core { get; private set; }

            public override void InitializeComponent(ICore core)
            {
                this.Core = core;
                base.InitializeComponent(core);
            }

            protected override async Task OnRun()
            {
                Logger.Write(this, LogLevel.Debug, "Creating encoder.");
                using (var encoder = EncoderFactory.CreateEncoder(this.EncoderItems))
                {
                    Logger.Write(this, LogLevel.Debug, "Starting encoder.");
                    using (var monitor = new BassEncoderMonitor(encoder, this.Visible, this.CancellationToken))
                    {
                        monitor.StatusChanged += this.OnStatusChanged;
                        try
                        {
                            await this.WithSubTask(monitor,
                                () => monitor.Encode()
                            ).ConfigureAwait(false);
                        }
                        finally
                        {
                            monitor.StatusChanged -= this.OnStatusChanged;
                        }
                        this.EncoderItems = monitor.EncoderItems.Values.ToArray();
                    }
                }
                Logger.Write(this, LogLevel.Debug, "Encoder completed successfully.");
            }

            protected virtual void OnStatusChanged(object sender, BassEncoderMonitorEventArgs e)
            {
                if (e.EncoderItem.Status == EncoderItemStatus.Complete)
                {
                    if (this.Behaviour.CopyTags.Value)
                    {
                        var task = this.CopyTags(e.EncoderItem);
                    }
                }
            }

            protected virtual async Task CopyTags(EncoderItem encoderItem)
            {
                try
                {
                    Logger.Write(this, LogLevel.Debug, "Copying tags from \"{0}\" to \"{1}\".", encoderItem.InputFileName, encoderItem.OutputFileName);
                    var fileData = this.GetFileData(encoderItem);
                    using (var task = new WriteFileMetaDataTask(encoderItem.OutputFileName, fileData.MetaDatas))
                    {
                        task.InitializeComponent(this.Core);
                        await task.Run().ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    Logger.Write(this, LogLevel.Warn, "Failed to copy tags from \"{0}\" to \"{1}\": {2}", encoderItem.InputFileName, encoderItem.OutputFileName, e.Message);
                    await this.OnError(e).ConfigureAwait(false);
                }
            }

            protected virtual IFileData GetFileData(EncoderItem encoderItem)
            {
                return this.FileDatas.FirstOrDefault(fileData => string.Equals(fileData.FileName, encoderItem.InputFileName, StringComparison.OrdinalIgnoreCase));
            }

            protected override void OnCancellationRequested()
            {
                this.CancellationToken.Cancel();
                base.OnCancellationRequested();
            }
        }
    }
}
