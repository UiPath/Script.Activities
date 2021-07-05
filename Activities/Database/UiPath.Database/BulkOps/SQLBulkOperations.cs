using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Data.Common;

namespace UiPath.Database.BulkOps
{
    public class SQLBulkOperations : IBulkOperations
    {
        public DbConnection Connection { get; set; }
        public string TableName { get; set; }
        public Type BulkCopyType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void WriteToServer(DataTable dataTable)
        {

            var conn = new SqlConnection(Connection.ConnectionString);
            conn.Open();
            // Set up the bulk copy object
            using (SqlBulkCopy bulkCopy = new SqlBulkCopy(conn))
            {
                bulkCopy.DestinationTableName = TableName;
                bulkCopy.WriteToServer(dataTable);
            }
        }
    }
}