﻿using System;
using System.Activities;
using System.ComponentModel;
using System.Data;
using System.Net;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Markup;
using UiPath.Database.Activities.Properties;
using UiPath.Robot.Activities.Api;

namespace UiPath.Database.Activities
{
    public class BulkUpdate : AsyncTaskCodeActivity
    {
        [DefaultValue(null)]
        [LocalizedCategory(nameof(Resources.ConnectionConfiguration))]
        [RequiredArgument]
        [OverloadGroup("New Database Connection")]
        [LocalizedDisplayName(nameof(Resources.ProviderNameDisplayName))]
        public InArgument<string> ProviderName { get; set; }

        [DependsOn(nameof(ProviderName))]
        [DefaultValue(null)]
        [LocalizedCategory(nameof(Resources.ConnectionConfiguration))]
        [OverloadGroup("New Database Connection")]
        [LocalizedDisplayName(nameof(Resources.ConnectionStringDisplayName))]
        public InArgument<string> ConnectionString { get; set; }

        [DefaultValue(null)]
        [DependsOn(nameof(ProviderName))]
        [LocalizedCategory(nameof(Resources.ConnectionConfiguration))]
        [OverloadGroup("New Database Connection")]
        [LocalizedDisplayName(nameof(Resources.ConnectionSecureStringDisplayName))]
        public InArgument<SecureString> ConnectionSecureString { get; set; }

        [RequiredArgument]
        [LocalizedCategory(nameof(Resources.ConnectionConfiguration))]
        [OverloadGroup("Existing Database Connection")]
        [LocalizedDisplayName(nameof(Resources.ExistingDbConnectionDisplayName))]
        public InArgument<DatabaseConnection> ExistingDbConnection { get; set; }

        [LocalizedCategory(nameof(Resources.ConnectionConfiguration))]
        [OverloadGroup("Existing Database Connection")]
        [LocalizedDisplayName(nameof(Resources.BulkUpdateFlag))]
        [DefaultValue(true)]
        public bool BulkUpdateFlag { get; set; } = true;

        [LocalizedCategory(nameof(Resources.Input))]
        [RequiredArgument]
        [DefaultValue(null)]
        [LocalizedDisplayName(nameof(Resources.TableNameDisplayName))]
        public InArgument<string> TableName { get; set; }

        [LocalizedCategory(nameof(Resources.Input))]
        [RequiredArgument]
        [DefaultValue(null)]
        [LocalizedDisplayName(nameof(Resources.ColumnNamesDisplayName))]
        public InArgument<string[]> ColumnNames { get; set; }

        [LocalizedCategory(nameof(Resources.Input))]
        [DefaultValue(null)]
        [RequiredArgument]
        [LocalizedDisplayName(nameof(Resources.DataTableDisplayName))]
        public InArgument<DataTable> DataTable { get; set; }

        [LocalizedCategory(nameof(Resources.Common))]
        [LocalizedDisplayName(nameof(Resources.ContinueOnErrorDisplayName))]
        public InArgument<bool> ContinueOnError { get; set; }

        [LocalizedCategory(nameof(Resources.Output))]
        [LocalizedDisplayName(nameof(Resources.AffectedRecordsDisplayName))]
        public OutArgument<long> AffectedRecords { get; set; }

        private DatabaseConnection DbConnection = null;

        private void HandleException(Exception ex, bool continueOnError)
        {
            if (continueOnError) return;
            throw ex;
        }

        protected async override Task<Action<AsyncCodeActivityContext>> ExecuteAsync(AsyncCodeActivityContext context, CancellationToken cancellationToken)
        {
            DataTable dataTable = null;
            string connString = null;
            SecureString connSecureString = null;
            string provName = null;
            string tableName = null;
            string[] columnNames = null;
            DatabaseConnection existingConnection = null;
            long affectedRecords = 0;
            IExecutorRuntime executorRuntime = null;
            try
            {
                existingConnection = DbConnection = ExistingDbConnection.Get(context);
                connString = ConnectionString.Get(context);
                provName = ProviderName.Get(context);
                tableName = TableName.Get(context);
                dataTable = DataTable.Get(context);
                columnNames = ColumnNames.Get(context);
                executorRuntime = context.GetExtension<IExecutorRuntime>();
                connSecureString = ConnectionSecureString.Get(context);


                if (DbConnection == null && connString == null && connSecureString == null)
                {
                    throw new ArgumentNullException(Resources.ConnectionMustBeSet);
                }

                affectedRecords = await Task.Run(() =>
                {
                    DbConnection = DbConnection ?? new DatabaseConnection().Initialize(connString != null ? connString : new NetworkCredential("", connSecureString).Password, provName);
                    if (DbConnection == null)
                    {
                        return 0;
                    }
                    if (executorRuntime != null && executorRuntime.HasFeature(ExecutorFeatureKeys.LogMessage))
                        return DbConnection.BulkUpdateDataTable(BulkUpdateFlag, tableName, dataTable, columnNames, executorRuntime);
                    else
                        return DbConnection.BulkUpdateDataTable(BulkUpdateFlag, tableName, dataTable, columnNames);
                });

            }
            catch (Exception ex)
            {
                HandleException(ex, ContinueOnError.Get(context));
            }
            finally
            {
                if (existingConnection == null)
                {
                    DbConnection?.Dispose();
                }
            }

            return asyncCodeActivityContext =>
            {
                AffectedRecords.Set(asyncCodeActivityContext, affectedRecords);
            };
        }
    }
}
