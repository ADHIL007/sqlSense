using System.Data;
using Microsoft.Data.SqlClient;

namespace sqlSense.Services
{
    public partial class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        private static string ChangeDatabaseInConnectionString(string connectionString, string database)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = database
            };
            return builder.ConnectionString;
        }
    }
}
