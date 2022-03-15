﻿// Copyright (c)  maikebing All rights reserved.
//// Licensed under the MIT License, See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TDengineDriver;

namespace IoTSharp.Data.Taos
{
    /// <summary>
    ///     Represents a SQL statement to be executed against a Taos database.
    /// </summary>
    public class TaosCommand : DbCommand
    {
        private readonly Lazy<TaosParameterCollection> _parameters = new Lazy<TaosParameterCollection>(
            () => new TaosParameterCollection());
        private readonly DateTime _dt1970;
        private TaosConnection _connection;
        private string _commandText;
        private IntPtr _taos =>_connection._taos;
        /// <summary>
        ///     Initializes a new instance of the <see cref="TaosCommand" /> class.
        /// </summary>
        public TaosCommand()
        {
            _dt1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TaosCommand" /> class.
        /// </summary>
        /// <param name="commandText">The SQL to execute against the database.</param>
        public TaosCommand(string commandText)
            => CommandText = commandText;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TaosCommand" /> class.
        /// </summary>
        /// <param name="commandText">The SQL to execute against the database.</param>
        /// <param name="connection">The connection used by the command.</param>
        public TaosCommand(string commandText, TaosConnection connection)
            : this(commandText)
        {
            Connection = connection;
            CommandTimeout = connection.DefaultTimeout;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TaosCommand" /> class.
        /// </summary>
        /// <param name="commandText">The SQL to execute against the database.</param>
        /// <param name="connection">The connection used by the command.</param>
        /// <param name="transaction">The transaction within which the command executes.</param>
        public TaosCommand(string commandText, TaosConnection connection, TaosTransaction transaction)
            : this(commandText, connection)
            => Transaction = transaction;

        /// <summary>
        ///     Gets or sets a value indicating how <see cref="CommandText" /> is interpreted. Only
        ///     <see cref="CommandType.Text" /> is supported.
        /// </summary>
        /// <value>A value indicating how <see cref="CommandText" /> is interpreted.</value>
        public override CommandType CommandType
        {
            get => CommandType.Text;
            set
            {
                if (value != CommandType.Text)
                {
                    throw new ArgumentException($"Invalid CommandType{value}");
                }
            }
        }

        /// <summary>
        ///     Gets or sets the SQL to execute against the database.
        /// </summary>
        /// <value>The SQL to execute against the database.</value>
        public override string CommandText
        {
            get => _commandText;
            set
            {
                if (DataReader != null)
                {
                    throw new InvalidOperationException($"SetRequiresNoOpenReader{nameof(CommandText)}");
                }

                if (value != _commandText)
                {
                    _commandText = value;
                }
            }
        }

        /// <summary>
        ///     Gets or sets the connection used by the command.
        /// </summary>
        /// <value>The connection used by the command.</value>
        public new virtual TaosConnection Connection
        {
            get => _connection;
            set
            {
                if (DataReader != null)
                {
                    throw new InvalidOperationException($"SetRequiresNoOpenReader{nameof(Connection)}");
                }

                if (value != _connection)
                {

                    _connection?.RemoveCommand(this);
                    _connection = value;
                    value?.AddCommand(this);
                }
            }
        }

        /// <summary>
        ///     Gets or sets the connection used by the command. Must be a <see cref="TaosConnection" />.
        /// </summary>
        /// <value>The connection used by the command.</value>
        protected override DbConnection DbConnection
        {
            get => Connection;
            set => Connection = (TaosConnection)value;
        }

        /// <summary>
        ///     Gets or sets the transaction within which the command executes.
        /// </summary>
        /// <value>The transaction within which the command executes.</value>
        public new virtual TaosTransaction Transaction { get; set; }

        /// <summary>
        ///     Gets or sets the transaction within which the command executes. Must be a <see cref="TaosTransaction" />.
        /// </summary>
        /// <value>The transaction within which the command executes.</value>
        protected override DbTransaction DbTransaction
        {
            get => Transaction;
            set => Transaction = (TaosTransaction)value;
        }

        /// <summary>
        ///     Gets the collection of parameters used by the command.
        /// </summary>
        /// <value>The collection of parameters used by the command.</value>
        public new virtual TaosParameterCollection Parameters
            => _parameters.Value;

        /// <summary>
        ///     Gets the collection of parameters used by the command.
        /// </summary>
        /// <value>The collection of parameters used by the command.</value>
        protected override DbParameterCollection DbParameterCollection
            => Parameters;

        /// <summary>
        ///     Gets or sets the number of seconds to wait before terminating the attempt to execute the command. Defaults to 30.
        /// </summary>
        /// <value>The number of seconds to wait before terminating the attempt to execute the command.</value>
        /// <remarks>
        ///     The timeout is used when the command is waiting to obtain a lock on the table.
        /// </remarks>
        public override int CommandTimeout { get; set; } = 30;

        /// <summary>
        ///     Gets or sets a value indicating whether the command should be visible in an interface control.
        /// </summary>
        /// <value>A value indicating whether the command should be visible in an interface control.</value>
        public override bool DesignTimeVisible { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating how the results are applied to the row being updated.
        /// </summary>
        /// <value>A value indicating how the results are applied to the row being updated.</value>
        public override UpdateRowSource UpdatedRowSource { get; set; }

        /// <summary>
        ///     Gets or sets the data reader currently being used by the command, or null if none.
        /// </summary>
        /// <value>The data reader currently being used by the command.</value>
        protected internal virtual TaosDataReader DataReader { get; set; }

        /// <summary>
        ///     Releases any resources used by the connection and closes it.
        /// </summary>
        /// <param name="disposing">
        ///     true to release managed and unmanaged resources; false to release only unmanaged resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {

            base.Dispose(disposing);
        }

        /// <summary>
        ///     Creates a new parameter.
        /// </summary>
        /// <returns>The new parameter.</returns>
        public new virtual TaosParameter CreateParameter()
            => new TaosParameter();

        /// <summary>
        ///     Creates a new parameter.
        /// </summary>
        /// <returns>The new parameter.</returns>
        protected override DbParameter CreateDbParameter()
            => CreateParameter();

        /// <summary>
        ///     Creates a prepared version of the command on the database.
        /// </summary>
        public override void Prepare()
        {
            if (_connection?.State != ConnectionState.Open)
            {
                throw new InvalidOperationException($"CallRequiresOpenConnection{nameof(Prepare)}");
            }

            if (string.IsNullOrEmpty(_commandText))
            {
                throw new InvalidOperationException($"CallRequiresSetCommandText{nameof(Prepare)}");
            }
 
        }

        /// <summary>
        ///     Executes the <see cref="CommandText" /> against the database and returns a data reader.
        /// </summary>
        /// <returns>The data reader.</returns>
        /// <exception cref="TaosException">A Taos error occurs during execution.</exception>
        public new virtual TaosDataReader ExecuteReader()
            => ExecuteReader(CommandBehavior.Default);


        internal long GetDateTimeFrom(DateTime dt,IntPtr _taos)
        {
            var val = dt.ToUniversalTime().Ticks - _dt1970.Ticks;
            //double tsp;
            var _dateTimePrecision = (TSDB_TIME_PRECISION)TDengine.ResultPrecision(_taos);
            switch (_dateTimePrecision)
            {
                /*
                * ticks为100纳秒，必须乘以10才能达到微秒级的区分度
                * 1秒s    = 1000毫秒ms
                * 1毫秒ms = 1000微秒us
                * 1微秒us = 1000纳秒ns
                * 因此， 1毫秒ms = 1000000纳秒ns = 10000ticks
                */
                case TSDB_TIME_PRECISION.TSDB_TIME_PRECISION_NANO:
                    val *= 100;
                    break;
                case TSDB_TIME_PRECISION.TSDB_TIME_PRECISION_MICRO:
                    val /= 10;
                    break;
                case TSDB_TIME_PRECISION.TSDB_TIME_PRECISION_MILLI:
                default:
                    val /= 10000;
                    break;
            }
            return val;
        }

        /// <summary>
        ///     Executes the <see cref="CommandText" /> against the database and returns a data reader.
        /// </summary>
        /// <param name="behavior">
        ///     A description of the results of the query and its effect on the database.
        ///     <para>
        ///         Only <see cref="CommandBehavior.Default" />, <see cref="CommandBehavior.SequentialAccess" />,
        ///         <see cref="CommandBehavior.SingleResult" />, <see cref="CommandBehavior.SingleRow" />, and
        ///         <see cref="CommandBehavior.CloseConnection" /> are supported.
        ///     </para>
        /// </param>
        /// <returns>The data reader.</returns>
        /// <exception cref="TaosException">A Taos error occurs during execution.</exception>
        public new virtual TaosDataReader ExecuteReader(CommandBehavior behavior)
        {
            if ((behavior & ~(CommandBehavior.Default | CommandBehavior.SequentialAccess | CommandBehavior.SingleResult
                              | CommandBehavior.SingleRow | CommandBehavior.CloseConnection)) != 0)
            {
                throw new ArgumentException($"InvalidCommandBehavior{behavior}");
            }

            if (DataReader != null)
            {
                throw new InvalidOperationException($"DataReaderOpen");
            }

            if (_connection?.State != ConnectionState.Open)
            {
                _connection.Open();
                if (_connection?.State != ConnectionState.Open)
                {
                    throw new InvalidOperationException($"CallRequiresOpenConnection{nameof(ExecuteReader)}");
                }
            }
            if (!_connection.SelectedDataBase)
            {
                _connection.ChangeDatabase(_connection.Database);
            }

            if (string.IsNullOrEmpty(_commandText))
            {
                throw new InvalidOperationException($"CallRequiresSetCommandText{nameof(ExecuteReader)}");
            }

            var unprepared = false;
            TaosDataReader dataReader = null;
            var closeConnection = (behavior & CommandBehavior.CloseConnection) != 0;
            try
            {
#if DEBUG
                Debug.WriteLine($"_commandText:{_commandText}");
#endif

                Task<IntPtr> code = null;
                bool isok = false;
                if (_parameters.IsValueCreated)
                {
                    var stmt = TDengine.StmtInit(_taos);
                    if (stmt == IntPtr.Zero)
                    {
                        int res = TDengine.StmtPrepare(stmt, _commandText);
                        if (res == 0)
                        {
                            Console.WriteLine("stmt prepare success");
                        }
                        else
                        {

                            Console.WriteLine("stmt prepare failed " + TDengine.StmtErrorStr(stmt));

                        }
                        var pms = _parameters.Value;
                        List<TAOS_BIND> binds = new List<TAOS_BIND>();
                        for (int i = 0; i < pms.Count; i++)
                        {
                            var tp = pms[i];
                            switch (TypeInfo.GetTypeCode(tp.Value?.GetType()))
                            {
                                case TypeCode.Boolean:
                                    binds.Add(TaosBind.BindBool((tp.Value as bool?).GetValueOrDefault()));
                                    break;
                                case TypeCode.Char:
                                    binds.Add(TaosBind.BindNchar(tp.Value as string));
                                    break;
                                case TypeCode.Byte:

                                case TypeCode.SByte:
                                    binds.Add(TaosBind.BindUTinyInt((tp.Value as byte?).GetValueOrDefault()));
                                    break;
                                case TypeCode.DateTime:
                                    var t0 = tp.Value as DateTime?;
                                    if (!t0.HasValue)
                                    {
                                        throw new ArgumentException($"InvalidArgumentOfDateTime{tp.Value}");
                                    }
                                    binds.Add(TaosBind.BindTimestamp(GetDateTimeFrom(t0.GetValueOrDefault(), _taos)));
                                    break;
                                case TypeCode.DBNull:
                                    binds.Add(TaosBind.BindNil());
                                    break;
                                case TypeCode.Single:
                                    binds.Add(TaosBind.BindFloat((tp.Value as float?).GetValueOrDefault()));
                                    break;
                                case TypeCode.Decimal:
                                case TypeCode.Double:
                                    binds.Add(TaosBind.BindDouble((tp.Value as double?).GetValueOrDefault()));
                                    break;
                                case TypeCode.Int16:
                                    binds.Add(TaosBind.BindSmallInt((tp.Value as short?).GetValueOrDefault()));
                                    break;
                                case TypeCode.Int32:
                                    binds.Add(TaosBind.BindInt((tp.Value as int?).GetValueOrDefault()));
                                    break;
                                case TypeCode.Int64:
                                    binds.Add(TaosBind.BindBigInt((tp.Value as long?).GetValueOrDefault()));
                                    break;
                                case TypeCode.UInt16:
                                    binds.Add(TaosBind.BindSmallInt((tp.Value as short?).GetValueOrDefault()));
                                    break;
                                case TypeCode.UInt32:
                                    binds.Add(TaosBind.BindUInt((tp.Value as uint?).GetValueOrDefault()));
                                    break;
                                case TypeCode.UInt64:
                                    binds.Add(TaosBind.BindUBigInt((tp.Value as ulong?).GetValueOrDefault()));
                                    break;
                                case TypeCode.String:
                                default:
                                    binds.Add(TaosBind.BindBinary(tp.Value as string));
                                    break;
                            }
                        }
                        int ret = TDengine.StmtBindParam(stmt, binds.ToArray());
                        if (ret == 0)
                        {
                            code = Task.Run(() =>
                            {
                                int re = TDengine.StmtExecute(stmt);
                                IntPtr res = TDengine.StmtUseResult(stmt);
                                return res;
                            });
                            isok = code.Wait(TimeSpan.FromSeconds(CommandTimeout));
                            if (isok == false)
                            {
                                string error = TDengine.StmtErrorStr(stmt);
                                TDengine.StmtClose(stmt);
                                TaosException.ThrowExceptionForRC(_commandText, new TaosErrorResult() { Code = TDengine.ErrorNo(_taos), Error = error });
                            }
                        }
                        TaosBind.FreeTaosBind(binds.ToArray());
                    }
                }
                else
                {
                    code = Task.Run(() => TDengine.Query(_taos, _commandText));
                    isok = code.Wait(TimeSpan.FromSeconds(CommandTimeout));
                    if (isok == false)
                    {
                        TDengine.StopQuery(_taos);
                    }
                }

                if (isok && TDengine.ErrorNo(code.Result) == 0)
                {
                    List<TDengineMeta> metas = TDengine.FetchFields(code.Result);
                    for (int j = 0; j < metas.Count; j++)
                    {
                        TDengineMeta meta = metas[j];
#if DEBUG
                        Debug.WriteLine("index:" + j + ", type:" + meta.type + ", typename:" + meta.TypeName() + ", name:" + meta.name + ", size:" + meta.size);
#endif
                    }
                    dataReader = new TaosDataReader(this, metas, closeConnection, code.Result);
                }
                else if (isok && TDengine.ErrorNo(code.Result) != 0)
                {
                    TaosException.ThrowExceptionForRC(_commandText, new TaosErrorResult() { Code = TDengine.ErrorNo(code.Result), Error = TDengine.Error(code.Result) });
                }
                else if (isok && code.Result == IntPtr.Zero)
                {
                    TaosException.ThrowExceptionForRC(_commandText, new TaosErrorResult() { Code = TDengine.ErrorNo(_taos), Error = TDengine.Error(_taos) });
                }
                else if (code.Status == TaskStatus.Running || !isok)
                {
                    TaosException.ThrowExceptionForRC(-10006, "Execute sql command timeout", null);
                }
                else if (code.IsCanceled)
                {
                    TaosException.ThrowExceptionForRC(-10003, "Command is Canceled", null);
                }
                else if (code.IsFaulted)
                {
                    TaosException.ThrowExceptionForRC(-10004, code.Exception.Message, code.Exception?.InnerException);
                }
                else
                {
                    TaosException.ThrowExceptionForRC(_commandText, new TaosErrorResult() { Code = -10007, Error = $"Unknow Exception" });
                }
            }
            catch when (unprepared)
            {
                throw;
            }
            return dataReader;
        }

        /// <summary>
        ///     Executes the <see cref="CommandText" /> against the database and returns a data reader.
        /// </summary>
        /// <param name="behavior">A description of query's results and its effect on the database.</param>
        /// <returns>The data reader.</returns>
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            => ExecuteReader(behavior);

        /// <summary>
        ///     Executes the <see cref="CommandText" /> asynchronously against the database and returns a data reader.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     Taos does not support asynchronous execution. Use write-ahead logging instead.
        /// </remarks>
        /// <seealso href="http://Taos.org/wal.html">Write-Ahead Logging</seealso>
        public new virtual Task<TaosDataReader> ExecuteReaderAsync()
            => ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None);

        /// <summary>
        ///     Executes the <see cref="CommandText" /> asynchronously against the database and returns a data reader.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     Taos does not support asynchronous execution. Use write-ahead logging instead.
        /// </remarks>
        /// <seealso href="http://Taos.org/wal.html">Write-Ahead Logging</seealso>
        public new virtual Task<TaosDataReader> ExecuteReaderAsync(CancellationToken cancellationToken)
            => ExecuteReaderAsync(CommandBehavior.Default, cancellationToken);

        /// <summary>
        ///     Executes the <see cref="CommandText" /> asynchronously against the database and returns a data reader.
        /// </summary>
        /// <param name="behavior">A description of query's results and its effect on the database.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     Taos does not support asynchronous execution. Use write-ahead logging instead.
        /// </remarks>
        /// <seealso href="http://Taos.org/wal.html">Write-Ahead Logging</seealso>
        public new virtual Task<TaosDataReader> ExecuteReaderAsync(CommandBehavior behavior)
            => ExecuteReaderAsync(behavior, CancellationToken.None);

        /// <summary>
        ///     Executes the <see cref="CommandText" /> asynchronously against the database and returns a data reader.
        /// </summary>
        /// <param name="behavior">A description of query's results and its effect on the database.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     Taos does not support asynchronous execution. Use write-ahead logging instead.
        /// </remarks>
        /// <seealso href="http://Taos.org/wal.html">Write-Ahead Logging</seealso>
        public new virtual Task<TaosDataReader> ExecuteReaderAsync(
            CommandBehavior behavior,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(ExecuteReader(behavior));
        }

        /// <summary>
        ///     Executes the <see cref="CommandText" /> asynchronously against the database and returns a data reader.
        /// </summary>
        /// <param name="behavior">A description of query's results and its effect on the database.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(
            CommandBehavior behavior,
            CancellationToken cancellationToken)
            => await ExecuteReaderAsync(behavior, cancellationToken);

        /// <summary>
        ///     Executes the <see cref="CommandText" /> against the database.
        /// </summary>
        /// <returns>The number of rows inserted, updated, or deleted. -1 for SELECT statements.</returns>
        /// <exception cref="TaosException">A Taos error occurs during execution.</exception>
        public override int ExecuteNonQuery()
        {
            if (_connection?.State != ConnectionState.Open)
            {
                throw new InvalidOperationException($"CallRequiresOpenConnection{nameof(ExecuteNonQuery)}");
            }
            if (_commandText == null)
            {
                throw new InvalidOperationException($"CallRequiresSetCommandText{nameof(ExecuteNonQuery)}");
            }
            using (var reader = ExecuteReader())
            {
                return reader.RecordsAffected;
            }
        }

        /// <summary>
        ///     Executes the <see cref="CommandText" /> against the database and returns the result.
        /// </summary>
        /// <returns>The first column of the first row of the results, or null if no results.</returns>
        /// <exception cref="TaosException">A Taos error occurs during execution.</exception>
        public override object ExecuteScalar()
        {
            if (_connection?.State != ConnectionState.Open)
            {
                throw new InvalidOperationException($"CallRequiresOpenConnection{nameof(ExecuteScalar)}");
            }
            if (_commandText == null)
            {
                throw new InvalidOperationException($"CallRequiresSetCommandText{nameof(ExecuteScalar)}");
            }

            using (var reader = ExecuteReader())
            {
                return reader.Read()
                    ? reader.GetValue(0)
                    : null;
            }
        }

        /// <summary>
        ///     Attempts to cancel the execution of the command. Does nothing.
        /// </summary>
        public override void Cancel()
        {
        }

      
    }
}