﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Reflection;

using Rebus.AdoNet.Schema;

namespace Rebus.AdoNet.Dialects
{
	/// <summary>
	/// Abstract/Base class for Sql Dialects.
	/// </summary>
	/// <remarks>
	/// Partially based on NHibernate's Dialect code.
	/// </remarks>
	public abstract class SqlDialect
	{
		#region Constants & Fields
		/// <summary></summary>
		public const char Dot = '.';

		/// <summary> Characters used for quoting sql identifiers </summary>
		public const string PossibleQuoteChars = "`'\"[";

		/// <summary> Characters used for closing quoted sql identifiers </summary>
		public const string PossibleClosedQuoteChars = "`'\"]";

		#endregion

		#region Properties
		public virtual ushort Priority => ushort.MaxValue;
		#endregion

		#region Get Database Version

		public virtual string GetDatabaseVersion(IDbConnection connection)
		{
			var result = connection.ExecuteScalar(
				@"SELECT CHARACTER_VALUE " +
				@"FROM INFORMATION_SCHEMA.SQL_IMPLEMENTATION_INFO " +
				@"WHERE IMPLEMENTATION_INFO_NAME='DBMS VERSION'");

			return Convert.ToString(result);
		}

		#endregion

		public abstract bool SupportsThisDialect(IDbConnection connection);

		#region Database type mapping support
		private readonly TypeNames _typeNames = new TypeNames();

		/// <summary>
		/// Subclasses register a typename for the given type code and maximum
		/// column length. <c>$l</c> in the type name will be replaced by the column
		/// length (if appropriate)
		/// </summary>
		/// <param name="code">The typecode</param>
		/// <param name="capacity">Maximum length of database type</param>
		/// <param name="name">The database type name</param>
		protected void RegisterColumnType(DbType code, uint capacity, string name)
		{
			_typeNames.Put(code, capacity, name);
		}

		/// <summary>
		/// Suclasses register a typename for the given type code. <c>$l</c> in the 
		/// typename will be replaced by the column length (if appropriate).
		/// </summary>
		/// <param name="code">The typecode</param>
		/// <param name="name">The database type name</param>
		protected void RegisterColumnType(DbType code, string name)
		{
			_typeNames.Put(code, name);
		}

		/// <summary>
		/// Get the name of the database type associated with the given
		/// <see cref="SqlType"/>.
		/// </summary>
		/// <param name="sqlType">The SqlType </param>
		/// <param name="length">The datatype length </param>
		/// <param name="precision">The datatype precision </param>
		/// <param name="scale">The datatype scale </param>
		/// <returns>The database type name used by ddl.</returns>
		public virtual string GetTypeName(DbType type, uint length, uint precision, uint scale)
		{
			string result = _typeNames.Get(type, length, precision, scale);
			if (result == null)
			{
				throw new ArgumentOutOfRangeException($"No type mapping for DbType {type} of length {length}");
			}
			return result;
		}

		/// <summary>
		/// Gets the name of the longest registered type for a particular DbType.
		/// </summary>
		/// <param name="dbType"></param>
		/// <returns></returns>
		public virtual string GetLongestTypeName(DbType dbType)
		{
			return _typeNames.GetLongest(dbType);
		}

		#endregion

		#region Identifier quoting support

		/// <summary>
		/// The opening quote for a quoted identifier.
		/// </summary>
		public virtual char OpenQuote
		{
			get { return '"'; }
		}

		/// <summary>
		/// The closing quote for a quoted identifier.
		/// </summary>
		public virtual char CloseQuote
		{
			get { return '"'; }
		}

		/// <summary>
		/// Checks to see if the name has been quoted.
		/// </summary>
		/// <param name="name">The name to check if it is quoted</param>
		/// <returns>true if name is already quoted.</returns>
		/// <remarks>
		/// The default implementation is to compare the first character
		/// to Dialect.OpenQuote and the last char to Dialect.CloseQuote
		/// </remarks>
		public virtual bool IsQuoted(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return false;
			}
			return (name[0] == OpenQuote && name[name.Length - 1] == CloseQuote);
		}

		public virtual string Qualify(string catalog, string schema, string table)
		{
			StringBuilder qualifiedName = new StringBuilder();

			if (!string.IsNullOrEmpty(catalog))
			{
				qualifiedName.Append(catalog).Append(Dot);
			}
			if (!string.IsNullOrEmpty(schema))
			{
				qualifiedName.Append(schema).Append(Dot);
			}
			return qualifiedName.Append(table).ToString();
		}

		/// <summary>
		/// Quotes a name.
		/// </summary>
		/// <param name="name">The string that needs to be Quoted.</param>
		/// <returns>A QuotedName </returns>
		/// <remarks>
		/// <p>
		/// This method assumes that the name is not already Quoted.  So if the name passed
		/// in is <c>"name</c> then it will return <c>"""name"</c>.  It escapes the first char
		/// - the " with "" and encloses the escaped string with OpenQuote and CloseQuote. 
		/// </p>
		/// </remarks>
		protected virtual string Quote(string name)
		{
			string quotedName = name.Replace(OpenQuote.ToString(), new string(OpenQuote, 2));

			// in some dbs the Open and Close Quote are the same chars - if they are 
			// then we don't have to escape the Close Quote char because we already
			// got it.
			if (OpenQuote != CloseQuote)
			{
				quotedName = name.Replace(CloseQuote.ToString(), new string(CloseQuote, 2));
			}

			return OpenQuote + quotedName + CloseQuote;
		}

		/// <summary>
		/// Unquotes and unescapes an already quoted name
		/// </summary>
		/// <param name="quoted">Quoted string</param>
		/// <returns>Unquoted string</returns>
		/// <remarks>
		/// <p>
		/// This method checks the string <c>quoted</c> to see if it is 
		/// quoted.  If the string <c>quoted</c> is already enclosed in the OpenQuote
		/// and CloseQuote then those chars are removed.
		/// </p>
		/// <p>
		/// After the OpenQuote and CloseQuote have been cleaned from the string <c>quoted</c>
		/// then any chars in the string <c>quoted</c> that have been escaped by doubling them
		/// up are changed back to a single version.
		/// </p>
		/// <p>
		/// The following quoted values return these results
		/// "quoted" = quoted
		/// "quote""d" = quote"d
		/// quote""d = quote"d 
		/// </p>
		/// <p>
		/// If this implementation is not sufficient for your Dialect then it needs to be overridden.
		/// MsSql2000Dialect is an example of where UnQuoting rules are different.
		/// </p>
		/// </remarks>
		public virtual string UnQuote(string quoted)
		{
			string unquoted;

			if (IsQuoted(quoted))
			{
				unquoted = quoted.Substring(1, quoted.Length - 2);
			}
			else
			{
				unquoted = quoted;
			}

			unquoted = unquoted.Replace(new string(OpenQuote, 2), OpenQuote.ToString());

			if (OpenQuote != CloseQuote)
			{
				unquoted = unquoted.Replace(new string(CloseQuote, 2), CloseQuote.ToString());
			}

			return unquoted;
		}

		/// <summary>
		/// Unquotes an array of Quoted Names.
		/// </summary>
		/// <param name="quoted">strings to Unquote</param>
		/// <returns>an array of unquoted strings.</returns>
		/// <remarks>
		/// This use UnQuote(string) for each string in the quoted array so
		/// it should not need to be overridden - only UnQuote(string) needs
		/// to be overridden unless this implementation is not sufficient.
		/// </remarks>
		public virtual string[] UnQuote(string[] quoted)
		{
			var unquoted = new string[quoted.Length];

			for (int i = 0; i < quoted.Length; i++)
			{
				unquoted[i] = UnQuote(quoted[i]);
			}

			return unquoted;
		}

		/// <summary>
		/// Quotes a name for being used as a aliasname
		/// </summary>
		/// <remarks>Original implementation calls <see cref="QuoteForTableName"/></remarks>
		/// <param name="aliasName">Name of the alias</param>
		/// <returns>A Quoted name in the format of OpenQuote + aliasName + CloseQuote</returns>
		/// <remarks>
		/// <p>
		/// If the aliasName is already enclosed in the OpenQuote and CloseQuote then this 
		/// method will return the aliasName that was passed in without going through any
		/// Quoting process.  So if aliasName is passed in already Quoted make sure that 
		/// you have escaped all of the chars according to your DataBase's specifications.
		/// </p>
		/// </remarks>
		public virtual string QuoteForAliasName(string aliasName)
		{
			return IsQuoted(aliasName) ? aliasName : Quote(aliasName);
		}

		/// <summary>
		/// Quotes a name for being used as a columnname
		/// </summary>
		/// <remarks>Original implementation calls <see cref="QuoteForTableName"/></remarks>
		/// <param name="columnName">Name of the column</param>
		/// <returns>A Quoted name in the format of OpenQuote + columnName + CloseQuote</returns>
		/// <remarks>
		/// <p>
		/// If the columnName is already enclosed in the OpenQuote and CloseQuote then this 
		/// method will return the columnName that was passed in without going through any
		/// Quoting process.  So if columnName is passed in already Quoted make sure that 
		/// you have escaped all of the chars according to your DataBase's specifications.
		/// </p>
		/// </remarks>
		public virtual string QuoteForColumnName(string columnName)
		{
			return IsQuoted(columnName) ? columnName : Quote(columnName);
		}

		/// <summary>
		/// Quotes a name for being used as a tablename
		/// </summary>
		/// <param name="tableName">Name of the table</param>
		/// <returns>A Quoted name in the format of OpenQuote + tableName + CloseQuote</returns>
		/// <remarks>
		/// <p>
		/// If the tableName is already enclosed in the OpenQuote and CloseQuote then this 
		/// method will return the tableName that was passed in without going through any
		/// Quoting process.  So if tableName is passed in already Quoted make sure that 
		/// you have escaped all of the chars according to your DataBase's specifications.
		/// </p>
		/// </remarks>
		public virtual string QuoteForTableName(string tableName)
		{
			return IsQuoted(tableName) ? tableName : Quote(tableName);
		}

		#endregion

		#region Parameter handling

		/// <summary>
		/// Gets the parameter placeholder.
		/// </summary>
		/// <value>
		/// The parameter placeholder.
		/// </value>
		public virtual string ParameterPlaceholder => "@";

		/// <summary>
		/// Escapes the parameter.
		/// </summary>
		/// <param name="parameterName">Name of the parameter.</param>
		/// <returns></returns>
		public virtual string EscapeParameter(string parameterName)
		{
			return string.Format("{0}{1}", ParameterPlaceholder, parameterName);
		}

		#endregion

		#region Infer database metadata

		/// <summary>
		/// Queries existing tables in the current database instance.
		/// </summary>
		public virtual IEnumerable<string> GetTableNames(IDbConnection connection)
		{
			return (connection as DbConnection).GetSchema("Tables")
				.Rows.OfType<DataRow>().Select(x => x["table_name"] as string)
				.ToArray();
		}

#if false
		public virtual string GetParameterMarkerFormat(IDbConnection connection)
		{
			return (connection as DbConnection)
				.GetSchema(DbMetaDataCollectionNames.DataSourceInformation)
				.Rows.OfType<DataRow>().First()[DbMetaDataColumnNames.ParameterMarkerFormat] as string;
		}
#endif

#endregion

#region Create Table

		public virtual string FormatCreateTable(AdoNetTable table)
		{
			var sb = new StringBuilder("CREATE TABLE ");

			sb.AppendFormat("{0} (", QuoteForTableName(table.Name));

			foreach (var column in table.Columns.ToArray())
			{
				sb.AppendFormat(" {0} {1} {2}{3}",
					QuoteForColumnName(column.Name),
					GetTypeName(column.DbType, column.Length, column.Precision, column.Scale),
					column.Nullable ? "" : "NOT NULL",
					(table.Columns.Last() == column) ? "" : ","
				);
			}

			if (table.PrimaryKey != null && table.PrimaryKey.Any())
			{
				var columns = table.PrimaryKey.Select(x => QuoteForColumnName(x)).Aggregate((cur, next) => cur + ", " + next);
				sb.AppendFormat(", PRIMARY KEY({0})", columns);
			}

			sb.Append(") ");

			sb.Append(";");

			if (table.Indexes != null && table.Indexes.Any())
			{
				foreach (var index in table.Indexes)
				{
					sb.AppendFormat("CREATE INDEX {0} ON {1} ({2});",
						!string.IsNullOrEmpty(index.Name) ? QuoteForTableName(index.Name) : "",
						QuoteForTableName(table.Name),
						index.Columns.Select(x => QuoteForColumnName(x)).Aggregate((cur, next) => cur + ", " + next)
					);
				}
			}

			return sb.ToString();
		}

#endregion

#region SqlDialects Registry
		private static readonly IList<SqlDialect> _dialects =
			typeof(SqlDialect).Assembly.GetTypes()
			.Where(x => x.IsClass && !x.IsAbstract)
			.Where(x => typeof(SqlDialect).IsAssignableFrom(x))
			.Select(x => (SqlDialect)Activator.CreateInstance(x))
			.ToList();

		public static void Register(SqlDialect dialect)
		{
			lock (_dialects)
			{
				_dialects.Add(dialect);
			}
		}

		public static IEnumerable<SqlDialect> GetAllDialects()
		{
			lock (_dialects)
			{
				return _dialects.OrderBy(x => x.Priority).ToArray();
			}
		}

		public static SqlDialect GetDialectFor(IDbConnection connection)
		{
			return GetAllDialects().FirstOrDefault(x => x.SupportsThisDialect(connection));
		}

#endregion
	}
}
