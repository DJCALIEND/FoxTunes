﻿using FoxTunes.Interfaces;
using System;
using System.Data;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FoxTunes
{
    public class LibraryHierarchyPopulator : PopulatorBase
    {
        public readonly object SyncRoot = new object();

        private LibraryHierarchyPopulator()
        {
            this.Command = new ThreadLocal<LibraryHierarchyPopulatorCommand>(true);
        }

        public LibraryHierarchyPopulator(IDatabaseContext databaseContext, IDbTransaction transaction)
            : this()
        {
            this.DatabaseContext = databaseContext;
            this.Transaction = transaction;
        }

        public IDatabaseContext DatabaseContext { get; private set; }

        public IDbTransaction Transaction { get; private set; }

        public IScriptingRuntime ScriptingRuntime { get; private set; }

        private ThreadLocal<LibraryHierarchyPopulatorCommand> Command { get; set; }

        public override void InitializeComponent(ICore core)
        {
            this.ScriptingRuntime = core.Components.ScriptingRuntime;
            base.InitializeComponent(core);
        }

        public void Populate(EnumerableDataReader reader)
        {
            this.Name = "Populating library hierarchies";
            this.Position = 0;
            this.Count = (
                this.DatabaseContext.GetQuery<LibraryHierarchyLevel>().Detach().Count() * this.DatabaseContext.GetQuery<LibraryItem>().Detach().Count()
            );

            var interval = Math.Max(Convert.ToInt32(this.Count * 0.01), 1);
            var position = 0;
            Parallel.ForEach(reader, this.ParallelOptions, record =>
            {
                var command = this.GetOrAddCommand();
                command.Parameters["libraryHierarchyId"] = record["LibraryHierarchy_Id"];
                command.Parameters["libraryHierarchyLevelId"] = record["LibraryHierarchyLevel_Id"];
                command.Parameters["libraryItemId"] = record["LibraryItem_Id"];
                command.Parameters["displayValue"] = this.ExecuteScript(command.ScriptingContext, record, "DisplayScript");
                command.Parameters["sortValue"] = this.ExecuteScript(command.ScriptingContext, record, "SortScript");
                command.Parameters["isLeaf"] = record["IsLeaf"];
                command.Command.ExecuteNonQuery();

                if (position % interval == 0)
                {
                    lock (this.SyncRoot)
                    {
                        var fileName = record["FileName"] as string;
                        this.Description = new FileInfo(fileName).Name;
                        this.Position = position;
                    }
                }

                Interlocked.Increment(ref position);
            });
        }

        private object ExecuteScript(IScriptingContext scriptingContext, EnumerableDataReader.EnumerableDataReaderRow record, string name)
        {
            var script = record[name] as string;
            if (string.IsNullOrEmpty(script))
            {
                return string.Empty;
            }
            var fileName = record["FileName"] as string;
            var metaData = new Dictionary<string, object>();
            for (var a = 0; true; a++)
            {
                var keyName = string.Format("Key_{0}", a);
                if (!record.ContainsKey(keyName))
                {
                    break;
                }
                var key = (record[keyName] as string).ToLower();
                var valueName = string.Format("Value_{0}_Value", a);
                var value = record[valueName] == DBNull.Value ? null : record[valueName];
                metaData.Add(key, value);
            }
            scriptingContext.SetValue("fileName", fileName);
            scriptingContext.SetValue("tag", metaData);
            try
            {
                return scriptingContext.Run(script);
            }
            catch (ScriptingException e)
            {
                return e.Message;
            }
        }

        private LibraryHierarchyPopulatorCommand GetOrAddCommand()
        {
            if (this.Command.IsValueCreated)
            {
                return this.Command.Value;
            }
            return this.Command.Value = new LibraryHierarchyPopulatorCommand(this.ScriptingRuntime, this.DatabaseContext, this.Transaction);
        }

        protected override void OnDisposing()
        {
            foreach (var command in this.Command.Values)
            {
                command.Dispose();
            }
            this.Command.Dispose();
            base.OnDisposing();
        }

        private class LibraryHierarchyPopulatorCommand : BaseComponent
        {
            public LibraryHierarchyPopulatorCommand(IScriptingRuntime scriptingRuntime, IDatabaseContext databaseContext, IDbTransaction transaction)
            {
                this.ScriptingContext = scriptingRuntime.CreateContext();
                var parameters = default(IDbParameterCollection);
                this.Command = databaseContext.Connection.CreateCommand(
                    Resources.AddLibraryHierarchyRecord,
                    new[] { "libraryHierarchyId", "libraryHierarchyLevelId", "libraryItemId", "displayValue", "sortValue", "isLeaf" },
                    out parameters
                );
                this.Command.Transaction = transaction;
                this.Parameters = parameters;
            }

            public IScriptingContext ScriptingContext { get; private set; }

            public IDbCommand Command { get; private set; }

            public IDbParameterCollection Parameters { get; private set; }

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
                this.ScriptingContext.Dispose();
                this.Command.Dispose();
            }
        }
    }
}