﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql;
using MySql.Data.MySqlClient;
using Npgsql;

namespace DatabaseWrapper
{
    /// <summary>
    /// Database client for MSSQL, Mysql, and PostgreSQL.
    /// </summary>
    public class DatabaseClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// The type of database.
        /// </summary>
        public DbTypes Type
        {
            get
            {
                return _DbType;
            }
        }

        /// <summary>
        /// The connection string used to connect to the database server.
        /// </summary>
        public string ConnectionString 
        { 
            get
            {
                return _ConnectionString;
            }
            private set
            {
                _ConnectionString = value;
            }
        }

        /// <summary>
        /// Enable or disable logging of queries using the Logger(string msg) method (default: false).
        /// </summary>
        public bool LogQueries = false;

        /// <summary>
        /// Enable or disable logging of query results using the Logger(string msg) method (default: false).
        /// </summary>
        public bool LogResults = false;

        /// <summary>
        /// Method to invoke when sending a log message.
        /// </summary>
        public Action<string> Logger = null;

        #endregion

        #region Private-Members

        private bool _Disposed = false;
        private string _ConnectionString = null;
        private string _SqliteFilename = null;
        private DbTypes _DbType;
        private string _ServerIp = null;
        private int _ServerPort = 0;
        private string _Username;
        private string _Password;
        private string _Instance;
        private string _DatabaseName;
          
        private Random _Random = new Random();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create an instance of the database client for a Sqlite database file.
        /// </summary>
        /// <param name="filename"></param>
        public DatabaseClient(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));

            _DbType = DbTypes.Sqlite;
            _SqliteFilename = filename;

            PopulateConnectionString();
        }

        /// <summary>
        /// Create an instance of the database client for Microsoft SQL Server, MySQL, or PostgreSQL.
        /// </summary>
        /// <param name="dbType">The type of database.</param>
        /// <param name="serverIp">The IP address or hostname of the database server.</param>
        /// <param name="serverPort">The TCP port of the database server.</param>
        /// <param name="username">The username to use when authenticating with the database server.</param>
        /// <param name="password">The password to use when authenticating with the database server.</param>
        /// <param name="instance">The instance on the database server (for use with Microsoft SQL Server).</param>
        /// <param name="database">The name of the database with which to connect.</param>
        public DatabaseClient(
            DbTypes dbType,
            string serverIp,
            int serverPort,
            string username,
            string password,
            string instance,
            string database)
        {
            if (String.IsNullOrEmpty(serverIp)) throw new ArgumentNullException(nameof(serverIp));
            if (serverPort < 0) throw new ArgumentOutOfRangeException(nameof(serverPort));
            if (String.IsNullOrEmpty(database)) throw new ArgumentNullException(nameof(database));

            _DbType = dbType;
            _ServerIp = serverIp;
            _ServerPort = serverPort;
            _Username = username;
            _Password = password;
            _Instance = instance;
            _DatabaseName = database;

            PopulateConnectionString();
        }

        /// <summary>
        /// Create an instance of the database client for Microsoft SQL Server, MySQL, or PostgreSQL.
        /// </summary>
        /// <param name="dbType">The type of database.</param>
        /// <param name="serverIp">The IP address or hostname of the database server.</param>
        /// <param name="serverPort">The TCP port of the database server.</param>
        /// <param name="username">The username to use when authenticating with the database server.</param>
        /// <param name="password">The password to use when authenticating with the database server.</param>
        /// <param name="instance">The instance on the database server (for use with Microsoft SQL Server).</param>
        /// <param name="database">The name of the database with which to connect.</param>
        public DatabaseClient(
            string dbType,
            string serverIp,
            int serverPort,
            string username,
            string password,
            string instance,
            string database)
        {
            if (String.IsNullOrEmpty(dbType)) throw new ArgumentNullException(nameof(dbType));
            if (String.IsNullOrEmpty(serverIp)) throw new ArgumentNullException(nameof(serverIp));
            if (serverPort < 0) throw new ArgumentOutOfRangeException(nameof(serverPort));
            if (String.IsNullOrEmpty(database)) throw new ArgumentNullException(nameof(database));

            switch (dbType.ToLower())
            {
                case "mssql":
                    _DbType = DbTypes.MsSql;
                    break;

                case "mysql":
                    _DbType = DbTypes.MySql;
                    break;

                case "pgsql":
                    _DbType = DbTypes.PgSql;
                    break;

                case "sqlite":
                    throw new ArgumentException("Sqlite is not a supported database type for this constructor.");

                default:
                    throw new ArgumentOutOfRangeException(nameof(dbType));
            }

            _ServerIp = serverIp;
            _ServerPort = serverPort;
            _Username = username;
            _Password = password;
            _Instance = instance;
            _DatabaseName = database;

            PopulateConnectionString();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Tear down the client and dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// List all tables in the database.
        /// </summary>
        /// <returns>List of strings, each being a table name.</returns>
        public List<string> ListTables()
        {
            string query = null;
            DataTable result = null;
            List<string> tableNames = new List<string>();

            switch (_DbType)
            {
                case DbTypes.MsSql:
                    query = MssqlHelper.LoadTableNamesQuery(_DatabaseName);
                    break;

                case DbTypes.MySql:
                    query = MysqlHelper.LoadTableNamesQuery();
                    break;

                case DbTypes.PgSql:
                    query = PgsqlHelper.LoadTableNamesQuery();
                    break;

                case DbTypes.Sqlite:
                    query = SqliteHelper.LoadTableNamesQuery();
                    break;
            }

            result = Query(query);

            if (result != null && result.Rows.Count > 0)
            {
                switch (_DbType)
                {
                    case DbTypes.MsSql:
                        foreach (DataRow curr in result.Rows)
                        {
                            tableNames.Add(curr["TABLE_NAME"].ToString());
                        }
                        break;

                    case DbTypes.MySql:
                        foreach (DataRow curr in result.Rows)
                        {
                            tableNames.Add(curr["Tables_in_" + _DatabaseName].ToString());
                        }
                        break;

                    case DbTypes.PgSql:
                        foreach (DataRow curr in result.Rows)
                        {
                            tableNames.Add(curr["tablename"].ToString());
                        }
                        break;

                    case DbTypes.Sqlite:
                        foreach (DataRow curr in result.Rows)
                        {
                            tableNames.Add(curr["TABLE_NAME"].ToString());
                        }
                        break;
                }
            }

            return tableNames;
        }

        /// <summary>
        /// Check if a table exists in the database.
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <returns>True if exists.</returns>
        public bool TableExists(string tableName)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            return ListTables().Contains(tableName);
        }

        /// <summary>
        /// Show the columns and column metadata from a specific table.
        /// </summary>
        /// <param name="tableName">The table to view.</param>
        /// <returns>A list of column objects.</returns>
        public List<Column> DescribeTable(string tableName)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            string query = null;
            DataTable result = null;
            List<Column> columns = new List<Column>();

            switch (_DbType)
            {
                case DbTypes.MsSql:
                    query = MssqlHelper.LoadTableColumnsQuery(_DatabaseName, tableName);
                    break;

                case DbTypes.MySql:
                    query = MysqlHelper.LoadTableColumnsQuery(_DatabaseName, tableName);
                    break;

                case DbTypes.PgSql:
                    query = PgsqlHelper.LoadTableColumnsQuery(_DatabaseName, tableName);
                    break;

                case DbTypes.Sqlite:
                    query = SqliteHelper.LoadTableColumnsQuery(tableName);
                    break;
            }
             
            result = Query(query);
            if (result != null && result.Rows.Count > 0)
            {
                foreach (DataRow currColumn in result.Rows)
                {
                    #region Process-Each-Column

                    /*
                    public bool PrimaryKey;
                    public string Name;
                    public string DataType;
                    public int? MaxLength;
                    public bool Nullable;
                    */

                    Column tempColumn = new Column();
                    
                    tempColumn.Name = currColumn["COLUMN_NAME"].ToString();

                    tempColumn.MaxLength = null;
                    if (currColumn.Table.Columns.Contains("CHARACTER_MAXIMUM_LENGTH"))
                    {
                        int maxLength = 0;
                        if (Int32.TryParse(currColumn["CHARACTER_MAXIMUM_LENGTH"].ToString(), out maxLength))
                        {
                            tempColumn.MaxLength = maxLength;
                        }
                    }

                    tempColumn.Type = DataTypeFromString(currColumn["DATA_TYPE"].ToString());

                    if (currColumn.Table.Columns.Contains("IS_NULLABLE"))
                    {
                        if (String.Compare(currColumn["IS_NULLABLE"].ToString(), "YES") == 0) tempColumn.Nullable = true;
                        else tempColumn.Nullable = false;
                    }
                    else if (currColumn.Table.Columns.Contains("IS_NOT_NULLABLE"))
                    {
                        tempColumn.Nullable = !(Convert.ToBoolean(currColumn["IS_NOT_NULLABLE"]));
                    }

                    switch (_DbType)
                    {
                        case DbTypes.MsSql:
                            if (currColumn["CONSTRAINT_NAME"] != null
                                && currColumn["CONSTRAINT_NAME"] != DBNull.Value
                                && !String.IsNullOrEmpty(currColumn["CONSTRAINT_NAME"].ToString()))
                            {
                                if (currColumn["CONSTRAINT_NAME"].ToString().ToLower().StartsWith("pk")) tempColumn.PrimaryKey = true; 
                            }
                            break; 

                        case DbTypes.MySql:
                            if (currColumn["COLUMN_KEY"] != null
                                && currColumn["COLUMN_KEY"] != DBNull.Value
                                && !String.IsNullOrEmpty(currColumn["COLUMN_KEY"].ToString()))
                            {
                                if (currColumn["COLUMN_KEY"].ToString().ToLower().Equals("pri")) tempColumn.PrimaryKey = true;
                            }
                            break;
                             
                        case DbTypes.PgSql:
                            if (currColumn["IS_PRIMARY_KEY"] != null
                                && currColumn["IS_PRIMARY_KEY"] != DBNull.Value
                                && !String.IsNullOrEmpty(currColumn["IS_PRIMARY_KEY"].ToString()))
                            {
                                if (currColumn["IS_PRIMARY_KEY"].ToString().ToLower().Equals("yes")) tempColumn.PrimaryKey = true;
                            }
                            break;

                        case DbTypes.Sqlite:
                            if (currColumn["IS_PRIMARY_KEY"] != null
                                && currColumn["IS_PRIMARY_KEY"] != DBNull.Value
                                && !String.IsNullOrEmpty(currColumn["IS_PRIMARY_KEY"].ToString()))
                            {
                                tempColumn.PrimaryKey = Convert.ToBoolean(currColumn["IS_PRIMARY_KEY"]);
                            }
                            break;
                    }
                     
                    if (!columns.Exists(c => c.Name.Equals(tempColumn.Name)))
                    {
                        columns.Add(tempColumn);
                    }

                    #endregion
                } 
            }

            return columns; 
        }

        /// <summary>
        /// Describe each of the tables in the database.
        /// </summary>
        /// <returns>Dictionary.  Key is table name, value is List of Column objects.</returns>
        public Dictionary<string, List<Column>> DescribeDatabase()
        { 
            DataTable result = new DataTable();
            Dictionary<string, List<Column>> ret = new Dictionary<string, List<Column>>();
            List<string> tableNames = ListTables();

            if (tableNames != null && tableNames.Count > 0)
            {
                foreach (string tableName in tableNames)
                {
                    ret.Add(tableName, DescribeTable(tableName));
                }
            }

            return ret; 
        }

        /// <summary>
        /// Create a table with a specified name.
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="columns">Columns.</param>
        public void CreateTable(string tableName, List<Column> columns)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null || columns.Count < 1) throw new ArgumentNullException(nameof(columns));

            string query = null;

            switch (_DbType)
            {
                case DbTypes.MsSql:
                    query = MssqlHelper.CreateTableQuery(tableName, columns);
                    break;

                case DbTypes.MySql:
                    query = MysqlHelper.CreateTableQuery(tableName, columns);
                    break;

                case DbTypes.PgSql:
                    query = PgsqlHelper.CreateTableQuery(tableName, columns);
                    break;

                case DbTypes.Sqlite:
                    query = SqliteHelper.CreateTableQuery(tableName, columns);
                    break;
            }

            DataTable result = Query(query); 
        }

        /// <summary>
        /// Drop the specified table.  
        /// </summary>
        /// <param name="tableName">The table to drop.</param>
        public void DropTable(string tableName)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            string query = null;

            switch (_DbType)
            {
                case DbTypes.MsSql:
                    query = MssqlHelper.DropTableQuery(tableName);
                    break;

                case DbTypes.MySql:
                    query = MysqlHelper.DropTableQuery(tableName);
                    break;

                case DbTypes.PgSql:
                    query = PgsqlHelper.DropTableQuery(tableName);
                    break;

                case DbTypes.Sqlite:
                    query = SqliteHelper.DropTableQuery(tableName);
                    break;
            }

            DataTable result = Query(query); 
        }

        /// <summary>
        /// Retrieve the name of the primary key column from a specific table.
        /// </summary>
        /// <param name="tableName">The table of which you want the primary key.</param>
        /// <returns>A string containing the column name.</returns>
        public string GetPrimaryKeyColumn(string tableName)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            List<Column> details = DescribeTable(tableName);
            if (details != null && details.Count > 0)
            {
                foreach (Column c in details)
                {
                    if (c.PrimaryKey) return c.Name;
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieve a list of the names of columns from within a specific table.
        /// </summary>
        /// <param name="tableName">The table of which ou want to retrieve the list of columns.</param>
        /// <returns>A list of strings containing the column names.</returns>
        public List<string> GetColumnNames(string tableName)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            List<Column> details = DescribeTable(tableName);
            List<string> columnNames = new List<string>();

            if (details != null && details.Count > 0)
            {
                foreach (Column c in details)
                {
                    columnNames.Add(c.Name);
                }
            }

            return columnNames;
        }

        /// <summary>
        /// Returns a DataTable containing at most one row with data from the specified table where the specified column contains the specified value.  Should only be used on key or unique fields.
        /// </summary>
        /// <param name="tableName">The table from which you wish to SELECT.</param>
        /// <param name="columnName">The column containing key or unique fields where a match is desired.</param>
        /// <param name="value">The value to match in the key or unique field column.  This should be an object that can be cast to a string value.</param>
        /// <returns>A DataTable containing at most one row.</returns>
        public DataTable GetUniqueObjectById(string tableName, string columnName, object value)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (String.IsNullOrEmpty(columnName)) throw new ArgumentNullException(nameof(columnName));
            if (value == null) throw new ArgumentNullException(nameof(value));

            Expression e = new Expression
            {
                LeftTerm = columnName,
                Operator = Operators.Equals,
                RightTerm = value.ToString()
            };

            return Select(tableName, null, 1, null, e, null);
        }

        /// <summary>
        /// Execute a SELECT query.
        /// </summary>
        /// <param name="tableName">The table from which you wish to SELECT.</param>
        /// <param name="indexStart">The starting index for retrieval; used for pagination in conjunction with maxResults and orderByClause.  orderByClause example: ORDER BY created DESC.</param>
        /// <param name="maxResults">The maximum number of results to retrieve.</param>
        /// <param name="returnFields">The fields you wish to have returned.  Null returns all.</param>
        /// <param name="filter">The expression containing the SELECT filter (i.e. WHERE clause data).</param>
        /// <param name="orderByClause">Specify an ORDER BY clause if desired.</param>
        /// <returns>A DataTable containing the results.</returns>
        public DataTable Select(string tableName, int? indexStart, int? maxResults, List<string> returnFields, Expression filter, string orderByClause)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
             
            string query = "";
            DataTable result; 

            switch (_DbType)
            {
                case DbTypes.MsSql:
                    query = MssqlHelper.SelectQuery(tableName, indexStart, maxResults, returnFields, filter, orderByClause);
                    break;

                case DbTypes.MySql:
                    query = MysqlHelper.SelectQuery(tableName, indexStart, maxResults, returnFields, filter, orderByClause);
                    break;
                     
                case DbTypes.PgSql:
                    query = PgsqlHelper.SelectQuery(tableName, indexStart, maxResults, returnFields, filter, orderByClause);
                    break;

                case DbTypes.Sqlite:
                    query = SqliteHelper.SelectQuery(tableName, indexStart, maxResults, returnFields, filter, orderByClause);
                    break;
            }

            result = Query(query);
            return result;
        }

        /// <summary>
        /// Execute an INSERT query.
        /// </summary>
        /// <param name="tableName">The table in which you wish to INSERT.</param>
        /// <param name="keyValuePairs">The key-value pairs for the row you wish to INSERT.</param>
        /// <returns>A DataTable containing the results.</returns>
        public DataTable Insert(string tableName, Dictionary<string, object> keyValuePairs)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (keyValuePairs == null || keyValuePairs.Count < 1) throw new ArgumentNullException(nameof(keyValuePairs));

            #region Variables

            string keys = "";
            string values = "";
            string query = "";
            int insertedId = 0;
            string retrievalQuery = "";
            DataTable result;

            #endregion

            #region Build-Key-Value-Pairs

            int added = 0;
            foreach (KeyValuePair<string, object> curr in keyValuePairs)
            {
                if (String.IsNullOrEmpty(curr.Key)) continue; 

                if (added == 0)
                {
                    #region First

                    keys += PreparedFieldname(curr.Key);
                    if (curr.Value != null)
                    {
                        if (curr.Value is DateTime || curr.Value is DateTime?)
                        {
                            values += "'" + DbTimestamp(_DbType, (DateTime)curr.Value) + "'";
                        }
                        else if (curr.Value is int || curr.Value is long || curr.Value is decimal)
                        {
                            values += curr.Value.ToString();
                        }
                        else
                        {
                            if (Helper.IsExtendedCharacters(curr.Value.ToString()))
                            {
                                values += PreparedUnicodeValue(curr.Value.ToString());
                            }
                            else
                            {
                                values += PreparedStringValue(curr.Value.ToString());
                            }
                        }
                    }
                    else
                    {
                        values += "null";
                    }

                    #endregion
                }
                else
                {
                    #region Subsequent

                    keys += "," + PreparedFieldname(curr.Key);
                    if (curr.Value != null)
                    {
                        if (curr.Value is DateTime || curr.Value is DateTime?)
                        {
                            values += ",'" + DbTimestamp(_DbType, (DateTime)curr.Value) + "'";
                        }
                        else if (curr.Value is int || curr.Value is long || curr.Value is decimal)
                        {
                            values += "," + curr.Value.ToString();
                        }
                        else
                        {
                            if (Helper.IsExtendedCharacters(curr.Value.ToString()))
                            {
                                values += "," + PreparedUnicodeValue(curr.Value.ToString());
                            }
                            else
                            {
                                values += "," + PreparedStringValue(curr.Value.ToString());
                            }
                        }

                    }
                    else
                    {
                        values += ",null";
                    }

                    #endregion
                }

                added++;
            }

            #endregion

            #region Build-INSERT-Query-and-Submit

            switch (_DbType)
            {
                case DbTypes.MsSql:
                    query = MssqlHelper.InsertQuery(tableName, keys, values);
                    break;

                case DbTypes.MySql:
                    query = MysqlHelper.InsertQuery(tableName, keys, values);
                    break;

                case DbTypes.PgSql:
                    query = PgsqlHelper.InsertQuery(tableName, keys, values);
                    break;

                case DbTypes.Sqlite:
                    query = SqliteHelper.InsertQuery(tableName, keys, values);
                    break;
            }

            result = Query(query);

            #endregion

            #region Post-Retrieval

            switch (_DbType)
            {
                case DbTypes.MsSql:
                    #region MsSql

                    //
                    // built into the query
                    //
                    break;

                    #endregion

                case DbTypes.MySql:
                    #region MySql

                    if (!Helper.DataTableIsNullOrEmpty(result))
                    {
                        bool idFound = false;

                        string primaryKeyColumn = GetPrimaryKeyColumn(tableName);

                        foreach (DataRow curr in result.Rows)
                        {
                            if (Int32.TryParse(curr["id"].ToString(), out insertedId))
                            {
                                idFound = true;
                                break;
                            }
                        }

                        if (!idFound)
                        {
                            result = null;
                        }
                        else
                        {
                            retrievalQuery = "SELECT * FROM `" + tableName + "` WHERE " + primaryKeyColumn + "=" + insertedId;
                            result = Query(retrievalQuery);
                        }
                    }
                    break;

                #endregion

                case DbTypes.PgSql:
                    #region PgSql

                    //
                    // built into the query
                    //
                    break;

                #endregion

                case DbTypes.Sqlite:
                    #region Sqlite

                    if (!Helper.DataTableIsNullOrEmpty(result))
                    {
                        bool idFound = false;

                        string primaryKeyColumn = GetPrimaryKeyColumn(tableName);

                        foreach (DataRow curr in result.Rows)
                        {
                            if (Int32.TryParse(curr["id"].ToString(), out insertedId))
                            {
                                idFound = true;
                                break;
                            }
                        }

                        if (!idFound)
                        {
                            result = null;
                        }
                        else
                        {
                            retrievalQuery = "SELECT * FROM `" + tableName + "` WHERE " + primaryKeyColumn + "=" + insertedId;
                            result = Query(retrievalQuery);
                        }
                    }
                    break;

                    #endregion

            }

            #endregion

            return result;
        }

        /// <summary>
        /// Execute an UPDATE query.
        /// For Microsoft SQL Server and PostgreSQL, the updated rows are returned.
        /// For MySQL and Sqlite, nothing is returned.
        /// </summary>
        /// <param name="tableName">The table in which you wish to UPDATE.</param>
        /// <param name="keyValuePairs">The key-value pairs for the data you wish to UPDATE.</param>
        /// <param name="filter">The expression containing the UPDATE filter (i.e. WHERE clause data).</param>
        /// <returns>For Microsoft SQL Server and PostgreSQL, a DataTable containing the results.  For MySQL and Sqlite, null.</returns>
        public DataTable Update(string tableName, Dictionary<string, object> keyValuePairs, Expression filter)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (keyValuePairs == null || keyValuePairs.Count < 1) throw new ArgumentNullException(nameof(keyValuePairs));

            #region Variables

            string query = "";
            string keyValueClause = "";
            DataTable result;  

            #endregion

            #region Build-Key-Value-Clause

            int added = 0;
            foreach (KeyValuePair<string, object> curr in keyValuePairs)
            {
                if (String.IsNullOrEmpty(curr.Key)) continue; 

                if (added == 0)
                {
                    if (curr.Value != null)
                    {
                        if (curr.Value is DateTime || curr.Value is DateTime?)
                        {
                            keyValueClause += PreparedFieldname(curr.Key) + "='" + DbTimestamp(_DbType, (DateTime)curr.Value) + "'";
                        }
                        else if (curr.Value is int || curr.Value is long || curr.Value is decimal)
                        {
                            keyValueClause += PreparedFieldname(curr.Key) + "=" + curr.Value.ToString();
                        }
                        else
                        {
                            if (Helper.IsExtendedCharacters(curr.Value.ToString()))
                            {
                                keyValueClause += PreparedFieldname(curr.Key) + "=" + PreparedUnicodeValue(curr.Value.ToString());
                            }
                            else
                            {
                                keyValueClause += PreparedFieldname(curr.Key) + "=" + PreparedStringValue(curr.Value.ToString());
                            }
                        }
                    }
                    else
                    {
                        keyValueClause += PreparedFieldname(curr.Key) + "= null";
                    }
                }
                else
                {
                    if (curr.Value != null)
                    {
                        if (curr.Value is DateTime || curr.Value is DateTime?)
                        {
                            keyValueClause += "," + PreparedFieldname(curr.Key) + "='" + DbTimestamp(_DbType, (DateTime)curr.Value) + "'";
                        }
                        else if (curr.Value is int || curr.Value is long || curr.Value is decimal)
                        {
                            keyValueClause += "," + PreparedFieldname(curr.Key) + "=" + curr.Value.ToString();
                        }
                        else
                        {
                            if (Helper.IsExtendedCharacters(curr.Value.ToString()))
                            {
                                keyValueClause += "," + PreparedFieldname(curr.Key) + "=" + PreparedUnicodeValue(curr.Value.ToString());
                            }
                            else
                            {
                                keyValueClause += "," + PreparedFieldname(curr.Key) + "=" + PreparedStringValue(curr.Value.ToString());
                            }
                        }
                    }
                    else
                    {
                        keyValueClause += "," + PreparedFieldname(curr.Key) + "= null";
                    }
                }
                added++;
            }

            #endregion

            #region Build-UPDATE-Query-and-Submit

            switch (_DbType)
            {
                case DbTypes.MsSql:
                    query = MssqlHelper.UpdateQuery(tableName, keyValueClause, filter);
                    break;
                    
                case DbTypes.MySql:
                    query = MysqlHelper.UpdateQuery(tableName, keyValueClause, filter);
                    break;

                case DbTypes.PgSql:
                    query = PgsqlHelper.UpdateQuery(tableName, keyValueClause, filter);
                    break;

                case DbTypes.Sqlite:
                    query = SqliteHelper.UpdateQuery(tableName, keyValueClause, filter);
                    break;
            }

            result = Query(query);

            #endregion
            
            return result;
        }

        /// <summary>
        /// Execute a DELETE query.
        /// </summary>
        /// <param name="tableName">The table in which you wish to DELETE.</param>
        /// <param name="filter">The expression containing the DELETE filter (i.e. WHERE clause data).</param>
        /// <returns>A DataTable containing the results.</returns>
        public DataTable Delete(string tableName, Expression filter)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            #region Variables

            string query = "";
            DataTable result; 

            #endregion

            #region Build-DELETE-Query-and-Submit

            switch (_DbType)
            {
                case DbTypes.MsSql:
                    query = MssqlHelper.DeleteQuery(tableName, filter);
                    break;
                     
                case DbTypes.MySql:
                    query = MysqlHelper.DeleteQuery(tableName, filter);
                    break;

                case DbTypes.PgSql:
                    query = PgsqlHelper.DeleteQuery(tableName, filter);
                    break;

                case DbTypes.Sqlite:
                    query = SqliteHelper.DeleteQuery(tableName, filter);
                    break;
            }

            result = Query(query);

            #endregion

            return result;
        }

        /// <summary>
        /// Empties a table completely.
        /// </summary>
        /// <param name="tableName">The table you wish to TRUNCATE.</param>
        public void Truncate(string tableName)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            string query = null;

            switch (_DbType)
            {
                case DbTypes.MsSql:
                    query = MssqlHelper.TruncateQuery(tableName);
                    break;
                case DbTypes.MySql:
                    query = MysqlHelper.TruncateQuery(tableName);
                    break;
                case DbTypes.PgSql:
                    query = PgsqlHelper.TruncateQuery(tableName);
                    break;
                case DbTypes.Sqlite:
                    query = SqliteHelper.TruncateQuery(tableName);
                    break;
                default:
                    throw new ArgumentException("Unknown database type: " + _DbType.ToString());
            }
             
            DataTable result = Query(query);

            return;
        }

        /// <summary>
        /// Execute a query.
        /// </summary>
        /// <param name="query">Database query defined outside of the database client.</param>
        /// <returns>A DataTable containing the results.</returns>
        public DataTable Query(string query)
        {
            if (String.IsNullOrEmpty(query)) throw new ArgumentNullException(query);
            DataTable result = new DataTable();

            if (LogQueries && Logger != null) Logger("[" + _DbType.ToString() + "] Query: " + query);

            switch (_DbType)
            {
                case DbTypes.MsSql:
                    #region Mssql

                    using (SqlConnection conn = new SqlConnection(_ConnectionString))
                    {
                        conn.Open();
                        SqlDataAdapter sda = new SqlDataAdapter(query, conn);
                        sda.Fill(result);
                        conn.Dispose();
                        conn.Close();
                    }

                    break;

                #endregion

                case DbTypes.MySql:
                    #region Mysql

                    using (MySqlConnection conn = new MySqlConnection(_ConnectionString))
                    {
                        conn.Open();
                        MySqlCommand cmd = new MySqlCommand();
                        cmd.Connection = conn;
                        cmd.CommandText = query;
                        MySqlDataAdapter sda = new MySqlDataAdapter(cmd);
                        DataSet ds = new DataSet();
                        sda.Fill(ds);
                        if (ds != null)
                        {
                            if (ds.Tables != null)
                            {
                                if (ds.Tables.Count > 0)
                                {
                                    result = ds.Tables[0];
                                }
                            }
                        }

                        conn.Close();
                    }

                    break;

                #endregion

                case DbTypes.PgSql:
                    #region Pgsql

                    using (NpgsqlConnection conn = new NpgsqlConnection(_ConnectionString))
                    {
                        conn.Open();
                        NpgsqlDataAdapter da = new NpgsqlDataAdapter(query, conn);
                        DataSet ds = new DataSet();
                        da.Fill(ds);

                        if (ds != null && ds.Tables != null && ds.Tables.Count > 0)
                        {
                            result = ds.Tables[0];
                        }

                        conn.Close();
                    }

                    break;

                #endregion

                case DbTypes.Sqlite:
                    #region Sqlite

                    using (SQLiteConnection conn = new SQLiteConnection(_ConnectionString))
                    {
                        conn.Open();

                        using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                        {
                            using (SQLiteDataReader rdr = cmd.ExecuteReader())
                            {
                                result.Load(rdr);
                            }
                        }

                        conn.Close();
                    }

                    break;

                #endregion 

                default:
                    throw new ArgumentException("Unknown database type: " + _DbType.ToString());
            }

            if (LogResults && Logger != null)
            {
                if (result != null)
                {
                    Logger("[" + _DbType.ToString() + "] Query result: " + result.Rows.Count + " rows");
                }
                else
                {
                    Logger("[" + _DbType.ToString() + "] Query result: null");
                }
            }

            return result;
        }

        /// <summary>
        /// Create a string timestamp from the given DateTime for the database of the instance type.
        /// </summary>
        /// <param name="ts">DateTime.</param>
        /// <returns>A string with timestamp formatted for the database of the instance type.</returns>
        public string Timestamp(DateTime ts)
        {
            switch (_DbType)
            {
                case DbTypes.MsSql:
                case DbTypes.PgSql:
                    return ts.ToString("MM/dd/yyyy hh:mm:ss.fffffff tt");

                case DbTypes.MySql:
                case DbTypes.Sqlite:
                    return ts.ToString("yyyy-MM-dd HH:mm:ss.ffffff");

                default:
                    return null;
            }
        }

        /// <summary>
        /// Sanitize an input string.
        /// </summary>
        /// <param name="s">The value to sanitize.</param>
        /// <returns>A sanitized string.</returns>
        public string SanitizeString(string s)
        {
            if (String.IsNullOrEmpty(s)) return s;

            switch (_DbType)
            {
                case DbTypes.MsSql:
                    return MssqlHelper.SanitizeString(s);
                    
                case DbTypes.MySql:
                    return MysqlHelper.SanitizeString(s);

                case DbTypes.PgSql:
                    return PgsqlHelper.SanitizeString(s);

                case DbTypes.Sqlite:
                    return SqliteHelper.SanitizeString(s);
            }

            throw new Exception("Unknown database type");
        }
         
        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose of the object.
        /// </summary>
        /// <param name="disposing">Disposing of resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed)
            {
                return;
            }

            if (disposing)
            { 
                // placeholder
            }

            _Disposed = true;
        }

        private void PopulateConnectionString()
        {
            _ConnectionString = "";

            switch (_DbType)
            {
                case DbTypes.MsSql:
                    _ConnectionString = MssqlHelper.ConnectionString(_ServerIp, _ServerPort, _Username, _Password, _Instance, _DatabaseName);
                    break;

                case DbTypes.MySql:
                    _ConnectionString = MysqlHelper.ConnectionString(_ServerIp, _ServerPort, _Username, _Password, _DatabaseName);
                    break;

                case DbTypes.PgSql:
                    _ConnectionString = PgsqlHelper.ConnectionString(_ServerIp, _ServerPort, _Username, _Password, _DatabaseName);
                    break;

                case DbTypes.Sqlite:
                    _ConnectionString = SqliteHelper.ConnectionString(_SqliteFilename);
                    break;
            }

            return;
        }
          
        private string PreparedFieldname(string s)
        {
            switch (_DbType)
            {
                case DbTypes.MsSql:
                    return "[" + s + "]";

                case DbTypes.MySql:
                    return "`" + s + "`";

                case DbTypes.PgSql:
                    return "\"" + s + "\"";

                case DbTypes.Sqlite:
                    return "`" + s + "`";
            }

            return null;
        }

        private string PreparedStringValue(string s)
        {
            switch (_DbType)
            {
                case DbTypes.MsSql:
                    return "'" + MssqlHelper.SanitizeString(s) + "'";

                case DbTypes.MySql:
                    return "'" + MysqlHelper.SanitizeString(s) + "'";

                case DbTypes.PgSql:
                    // uses $xx$ escaping
                    return PgsqlHelper.SanitizeString(s);

                case DbTypes.Sqlite:
                    return "'" + SqliteHelper.SanitizeString(s) + "'";
            }

            return null;
        }

        private string PreparedUnicodeValue(string s)
        {
            switch (_DbType)
            {
                case DbTypes.MsSql:
                    return "N" + PreparedStringValue(s);

                case DbTypes.MySql:
                    return "N" + PreparedStringValue(s);

                case DbTypes.PgSql:
                    return "U&" + PreparedStringValue(s);

                case DbTypes.Sqlite:
                    return "N" + PreparedStringValue(s);
            }

            return null;
        }

        private DataType DataTypeFromString(string s)
        {
            if (String.IsNullOrEmpty(s)) throw new ArgumentNullException(nameof(s));

            s = s.ToLower();
            if (s.Contains("(")) s = s.Substring(0, s.IndexOf("("));

            switch (s)
            {
                case "bigserial":               // pgsql
                case "bigint":                  // mssql
                    return DataType.Long;

                case "smallserial":             // pgsql
                case "smallest":                // pgsql
                case "tinyint":                 // mssql, mysql
                case "integer":                 // pgsql, sqlite
                case "int":                     // mssql, mysql
                case "smallint":                // mssql, mysql
                case "mediumint":               // mysql
                case "serial":                  // pgsql
                    return DataType.Int;

                case "real":                    // pgsql, sqlite
                case "double":                  // mysql
                case "double precision":        // pgsql
                case "float":                   // mysql
                    return DataType.Double;

                case "decimal":                 // mssql
                case "numeric":                 // mssql
                    return DataType.Decimal;

                case "timestamp without timezone":      // pgsql
                case "timestamp without time zone":     // pgsql
                case "timestamp with timezone":         // pgsql
                case "timestamp with time zone":        // pgsql
                case "time without timezone":           // pgsql
                case "time without time zone":          // pgsql
                case "time with timezone":              // pgsql
                case "time with time zone":             // pgsql
                case "time":                    // mssql, mysql
                case "date":                    // mssql, mysql
                case "datetime":                // mssql, mysql
                case "datetime2":               // mssql
                case "timestamp":               // mysql
                    return DataType.DateTime;

                case "character":               // pgsql
                case "char":                    // mssql, mysql, pgsql
                case "text":                    // mssql, mysql, pgsql, sqlite
                case "varchar":                 // mssql, mysql, pgsql
                    return DataType.Varchar;

                case "character varying":       // pgsql
                case "nchar":
                case "ntext":
                case "nvarchar":
                    return DataType.Nvarchar;   // mssql

                case "blob":
                    return DataType.Blob;       // sqlite

                default:
                    throw new ArgumentException("Unknown DataType: " + s);
            }
        }

        #endregion

        #region Public-Static-Methods

        /// <summary>
        /// Convert a DateTime to a string formatted for the specified database type.
        /// </summary>
        /// <param name="dbType">The type of database.</param>
        /// <param name="ts">The timestamp.</param>
        /// <returns>A string formatted for use with the specified database.</returns>
        public static string DbTimestamp(DbTypes dbType, DateTime ts)
        {
            switch (dbType)
            {
                case DbTypes.MsSql:
                case DbTypes.PgSql:
                case DbTypes.Sqlite:
                    return ts.ToString("MM/dd/yyyy hh:mm:ss.fffffff tt");

                case DbTypes.MySql:
                    return ts.ToString("yyyy-MM-dd HH:mm:ss.ffffff");

                default:
                    return null;
            }
        }

        /// <summary>
        /// Convert a DateTime to a string formatted for the specified database type.
        /// </summary>
        /// <param name="dbType">The type of database.</param>
        /// <param name="ts">The timestamp.</param>
        /// <returns>A string formatted for use with the specified database.</returns>
        public static string DbTimestamp(string dbType, DateTime ts)
        {
            if (String.IsNullOrEmpty(dbType)) throw new ArgumentNullException(nameof(dbType));
            switch (dbType.ToLower())
            {
                case "mssql":
                    return DbTimestamp(DbTypes.MsSql, ts);

                case "mysql":
                    return DbTimestamp(DbTypes.MySql, ts);

                case "pgsql":
                    return DbTimestamp(DbTypes.PgSql, ts);

                case "sqlite":
                    return DbTimestamp(DbTypes.Sqlite, ts);

                default:
                    throw new ArgumentOutOfRangeException(nameof(dbType));
            }
        }

        #endregion
    }
}
