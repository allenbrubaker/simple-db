using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Xml;
using System.IO;

namespace XQuery.Classes
{
	public static class DataHandler
	{
		public static DataSet DataSet = new DataSet();

   		#region Load
		static DataHandler()
		{
			LoadDataSet();
		}

		/// <summary>
		/// Create the DataSet from xml files (Schema.xml and TableName.xml)
		/// </summary>
		public static void LoadDataSet()
		{
			DataSet = new DataSet();
			DataSet.CaseSensitive = false;
			XmlDocument xSchema = new XmlDocument();

			// No data to load.
			if (!File.Exists("Data/Schema.xml"))
			{
				xSchema.LoadXml("<Schema />");
				Directory.CreateDirectory("Data/");
				xSchema.Save("Data/Schema.xml");
				return;
			}

			// Load each table definition in the schema file into DataSet.
			xSchema.Load("Data/Schema.xml");
			foreach (XmlNode x in xSchema.DocumentElement.ChildNodes)
			{
				DataTable table = new DataTable(x.Name);
				foreach (XmlNode childNode in x.ChildNodes)
				{
					table.Columns.Add(CreateColumn(childNode.Name, childNode.Attributes["Type"].Value));
				}

				// Load data from TableName.xml.
				string tablePath = "Data/" + x.Name + ".xml";
				XmlDocument xTable = new XmlDocument();
				if (!File.Exists(tablePath)) // Table definition exists in schema, but TableName.xml doesn't exist.  Create it.  
				{
					xTable.LoadXml("<" + x.Name + " />");
					xTable.Save(tablePath);
				}
				else
					xTable.Load(tablePath);

				foreach (XmlNode xRow in xTable.DocumentElement.ChildNodes)
				{
					DataRow row = table.NewRow();
					foreach (XmlNode xCol in xRow.ChildNodes)
					{
						row[xCol.Name] = xCol.InnerText;
					}
					table.Rows.Add(row);
				}
				
				DataSet.Tables.Add(table);
			}
			DataSet.AcceptChanges();	
		}
		#endregion

		#region Create

		/// <summary>
		/// Creates a new table: new Table.xml file, updates Schema.xml, and DataSet.
		/// </summary>
		public static void CreateTable(string tableName, List<string> columns, List<string> types)
		{

			if (DataSet.Tables.Contains(tableName))
				throw new InvalidQueryException("Cannot create an already existing table: " + tableName);
			if (columns.Count != types.Count)
				throw new InvalidQueryException("There must be exactly the same number of columns and types.");
			if (columns.Count == 0)
				throw new InvalidQueryException("Create at least one pair: Type Column.");

			// Create DataTable and append to DataSet
			DataTable table = new DataTable(tableName);
			for (int i = 0; i < columns.Count; ++i)
			{
				table.Columns.Add(CreateColumn(columns[i], types[i]));
			}
			DataSet.Tables.Add(table);
			DataSet.AcceptChanges();


			// Add to Schema.xml file.
			XmlDocument xmlDoc = new XmlDocument();
			xmlDoc.Load("Data/Schema.xml");
			XmlElement root = xmlDoc.DocumentElement;
			XmlElement node = xmlDoc.CreateElement(tableName);
			foreach (DataColumn column in table.Columns)
			{
				XmlElement childNode = xmlDoc.CreateElement(column.ColumnName);
				XmlAttribute typeAttribute = xmlDoc.CreateAttribute("Type");
				typeAttribute.Value = column.DataType.FullName;
				childNode.Attributes.Append(typeAttribute);
				node.AppendChild(childNode);
			}
			root.AppendChild(node);
			xmlDoc.Save("Data/Schema.xml");


			// Create the new Table.xml file.
			string tablePath = "Data/" + tableName + ".xml";
			xmlDoc.LoadXml("<" + tableName + " />");
			xmlDoc.Save(tablePath);

			Log.Add(tableName, "Created");
		}


		#endregion

		#region Insert

		/// <summary>
		/// Inserts records into an existing table.  Update TableName.xml, update DataSet.
		/// </summary>
		public static void InsertValues(string tableName, List<string> columns, List<List<string>> values)
		{
			if (!DataSet.Tables.Contains(tableName))
				throw new InvalidQueryException("Cannot insert values into a non-existent table: " + tableName);
			
			DataTable table = DataSet.Tables[tableName];

			if (columns.Count == 0)
				foreach (DataColumn col in table.Columns)
					columns.Add(col.ColumnName);
			else
				foreach (string column in columns)
					if (!table.Columns.Contains(column))
						throw new InvalidQueryException("Cannot insert values into a non-existent column: " + column);

			// Update the DataSet
			List<DataRow> addedRows = new List<DataRow>();
			foreach (List<string> value in values)
			{
				if (value.Count != columns.Count) 
					throw new InvalidQueryException("Values do not match columns: \r\n" + string.Join("\t", columns.ToArray()) + "\r\n" + string.Join("\t", value.ToArray())  );
				
				DataRow row = table.NewRow();
				for (int i = 0; i < columns.Count; ++i)
				{
					//try { row[columns[i]] = Convert.ChangeType(value[i], table.Columns[columns[i]].DataType); }
					try { row.SetField(columns[i], value[i]); }
					catch { throw new InvalidQueryException("Invalid value for " + tableName + "." + columns[i] + ".  '" + value[i] + "' cannot be cast to " + table.Columns[columns[i]].DataType + "."); }
				}
				addedRows.Add(row);
				table.Rows.Add(row);
			}
			
			// Update the Table.xml
			XmlDocument xmlDoc = new XmlDocument();
			string tablePath = "Data/" + tableName + ".xml";
			xmlDoc.Load(tablePath);
			XmlElement root = xmlDoc.DocumentElement;
			foreach (DataRow row in addedRows)
			{
				XmlElement xRow = xmlDoc.CreateElement("Row");
				foreach (DataColumn column in table.Columns)
				{
					XmlNode xCol = xmlDoc.CreateElement(column.ColumnName);
					xCol.InnerText = row[column].ToString();
					xRow.AppendChild(xCol);
				}
				root.AppendChild(xRow);
			}
			xmlDoc.Save(tablePath);

			Log.Add(DataSet.Tables[tableName].TableName, "Inserted " + addedRows.Count + " rows");
		}
	#endregion

		#region Select


		/// <summary>
		/// Returns the joined result of all the tables in the From clause satisfying the On constraints.
		/// </summary>
		public static DataTable JoinTables(List<string> From, List<Expression> On)
		{
			DataTable result = new DataTable("QueryResult");

			// Test to make sure all tables in From are joined by On constraints.
			if (From.Count > 1)
			{
				List<string> linked = new List<string>();
				foreach (Expression exp in On)
				{
					string tableLeft = exp.Left.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries)[0].ToLower();
					string tableRight = exp.Right.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries)[0].ToLower();
					if (linked.Count == 0)
					{
						linked.Add(tableLeft);
						if (tableLeft != tableRight) linked.Add(tableRight);
					}
					else
					{
						if (linked.Contains(tableLeft))
							if (!linked.Contains(tableRight))
								linked.Add(tableRight);
						if (linked.Contains(tableRight))
							if (!linked.Contains(tableLeft))
								linked.Add(tableLeft);
					}
				}
				foreach (string from in From)
				{
					string table = from.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries)[0].ToLower();
					if (!linked.Contains(table))
						throw new InvalidQueryException("Make sure all tables in From statement are joined:  " + table);
				}
			}

			result = DataSet.Tables[From[0]].Copy();
			foreach (DataColumn column in result.Columns)
			{
				column.ColumnName = result.TableName + "." + column.ColumnName;
			}

			for (int j = 1; j < From.Count; ++j)
			{
				DataTable join = DataSet.Tables[From[j]];

				// Determine criteria to match on for to-be-joined table.
				List<Expression> keysOfInterest = new List<Expression>();
				foreach (Expression key in On)
				{
					if (result.Columns.Contains(key.Left) && join.Columns.ContainsFullName(key.Right))
					{
						keysOfInterest.Add(key);
					}
					else if (result.Columns.Contains(key.Right) && join.Columns.ContainsFullName(key.Left))
					{
						// Make sure the left-most key is the key in the results table.
						string left = key.Left;
						key.Left = key.Right;
						key.Right = left;
						keysOfInterest.Add(key);
					}
				}


				// Copy column structure from to-be-joined table.
				foreach (DataColumn column in join.Columns)
					result.Columns.Add(CreateColumn(column.Table.TableName + "." + column.ColumnName, column.DataType.Name));


				// Copy rows from to-be-joined table (only if it matches the keysOfInterest criteria.)
				int count = result.Rows.Count;
				for (int i=0; i<count; ++i)
				{
					DataRow resultRow = result.Rows[0];
					foreach (DataRow joinRow in join.Rows)
					{
						bool isValidRow = true;
						foreach (Expression key in keysOfInterest)
						{
							if (!Compare(resultRow[key.Left], key.Operator, joinRow[key.Right.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries)[1]]))	
								isValidRow = false;
						}
						if (isValidRow)
						{
							DataRow newRow = result.NewRow();
							foreach (DataColumn column in result.Columns)
								newRow[column.ColumnName] = resultRow[column.ColumnName];
							foreach (DataColumn column in join.Columns)
								newRow[join.TableName + "." + column.ColumnName] = joinRow[column.ColumnName];
							result.Rows.Add(newRow);
						}
					}
					result.Rows.RemoveAt(0); // Change state to delete.  Remove when outside foreach loop.
				}
				result.AcceptChanges(); // Remove all deleted rows.  
			}

			return result;			
		}
			
		/// <summary>
		/// Filters the given DataTable depending on the Where constraints.
		/// </summary>
		public static DataTable FilterTable(DataTable table, List<List<Expression>> Where, bool deleteMatch = false)
		{
			if (Where.Count > 0)
			{
				for (int i=0; i<table.Rows.Count; ++i)
				{
					bool orMatch = false;
					foreach (List<Expression> andExpressions in Where)
					{
						bool andMatch = true;
						foreach (Expression exp in andExpressions)
						{
							andMatch = andMatch && Compare(table.Rows[i][exp.Left], exp.Operator, exp.Right);
						}
						orMatch = orMatch || andMatch;
					}

					// Keep the opposite of what was matched.
					if (deleteMatch)
						orMatch = !orMatch;

					if (!orMatch)
					{
						table.Rows.RemoveAt(i);
						--i;
					}
				}
			}
			return table;
		}

		/// <summary>
		/// Sorts the table via the given constraints in OrderBy recursively using insertion sort on array of indices.
		/// </summary>
		public static DataTable SortTable(DataTable table, List<string> OrderBy, List<string> direction)
		{
			foreach (string column in OrderBy)
				if (!table.Columns.Contains(column))
					throw new InvalidQueryException("Sorting an invalid column: " + column);

			List<int> rowIndices = new List<int>();
			for (int i=0; i< table.Rows.Count; ++i)
				rowIndices.Add(i);

			// Sort indices on each statement in OrderBy
			SortHelper(0, table.Rows.Count - 1, 0, ref rowIndices, table, OrderBy, direction);	

			// Clone table schema and add records in sorted order.
			DataTable sortedTable = table.Clone();
			foreach (int index in rowIndices)
			{
				sortedTable.ImportRow(table.Rows[index]);
			}
			return sortedTable;
		}

		private static void SortHelper(int min, int max, int sortIndex, ref List<int> rowIndices, DataTable table, List<string> OrderBy, List<string> direction)
		{
			if (max <= min || sortIndex > OrderBy.Count - 1)
				return;

			object val1, val2;

			// Insertion Sort
			for (int i = min; i <= max; ++i)
			{
				for (int j = i; j > min; --j)
				{
					val1 = table.Rows[rowIndices[j]][OrderBy[sortIndex]];
					val2 = table.Rows[rowIndices[j - 1]][OrderBy[sortIndex]];
					
					string sortDirection = direction[sortIndex].ToLower();
					if (sortDirection == "asc" || sortDirection == "ascending")
					{
						if (Compare(val1, "<", val2))
						{
							int temp = rowIndices[j];
							rowIndices[j] = rowIndices[j - 1];
							rowIndices[j - 1] = temp;
						}
						else break;
					}
					else if (sortDirection == "desc" || sortDirection == "descending")
					{
						if (Compare(val1, ">", val2))
						{
							int temp = rowIndices[j];
							rowIndices[j] = rowIndices[j - 1];
							rowIndices[j - 1] = temp;
						}
						else break;
					}
					else
						throw new InvalidQueryException("Invalid sort direction: " + sortDirection + ".\r\n Valid directions are desc, descending, asc, ascending");
				}
			}

			// Run through the sorted values and sort each group recursively.
			int p = min, q = min;
			while (q + 1 <= max)
			{
				val1 = table.Rows[rowIndices[p]][OrderBy[sortIndex]];
				val2 = table.Rows[rowIndices[q + 1]][OrderBy[sortIndex]];
				while (Compare(val1, "=", val2))
				{
					++q;
					if (q + 1 > max) break;
					val2 = table.Rows[rowIndices[q + 1]][OrderBy[sortIndex]];
				}
				SortHelper(p, q, sortIndex + 1, ref rowIndices, table, OrderBy, direction);
				q = p = q + 1;
			}
		}


		/// <summary>
		/// SelectTable(..) helper that takes the table, aggregates on functions specified in Select into groups specified by OrderBy.
		/// </summary>
		public static DataTable GroupTable(DataTable table, List<string> OrderBy, List<string> Select, List<string> From)
		{
			// Check to see if there are any aggregate functions.
			bool groupTable = false;
			foreach (string column in Select)
				if (column.Contains('!'))
					groupTable = true;
			if (!groupTable)
				return table;

			// Create result table with a column for each string in OrderBy and a column for each aggregate column.  
			DataTable result = new DataTable("QueryResult");
			string fullName;
			string aggregate;
			foreach (string column in OrderBy)
			{
				result.Columns.Add(CreateColumn(table.Columns[column].ColumnName, table.Columns[column].DataType.Name));
			}
			foreach (string column in Select)
			{
				if (column.Contains("!"))
				{
					aggregate = column.Split('!')[0].ToLower();
					if (!SyntaxProvider.Aggregates.Contains(aggregate))
						throw new InvalidQueryException("Unsupported aggregate function:  " + column);
					fullName = FullyQualifiedName(column.Split('!', ':')[1], From); // Remove alias and find fully qualified column name
					result.Columns.Add(CreateColumn(aggregate + "!" + table.Columns[fullName], aggregate == "count" ? "int" : table.Columns[fullName].DataType.Name));
				}
				else
				{
					fullName = FullyQualifiedName(column.Split(':')[0], From);
					if (!OrderBy.Exists(e => e.ToLower() == fullName.ToLower()))
						throw new InvalidQueryException("Cannot select non-aggregate column when not in OrderBy expression: " + column);
				}
			}

			// If no data exists in the original table, add a row to results table with default data.
			if (table.Rows.Count == 0)
			{
				result.Rows.Add(result.NewRow());
				return result;
			}


			int groupCount = 1;
			DataRow aggregator = result.NewRow();
			DataRow lastRow = table.NewRow();
			DataRow currentRow = table.NewRow();

			// Copy data from original table into aggregator.  
			foreach (DataColumn column in result.Columns)
			{
				if (!column.ColumnName.Contains("count!"))
				{
					fullName = column.ColumnName.Contains('!') ? column.ColumnName.Split('!')[1] : column.ColumnName;
					aggregator[column] = table.Rows[0][fullName];
				}
			}


			for (int i = 1; i < table.Rows.Count; ++i)
			{
				lastRow = table.Rows[i - 1];
				currentRow = table.Rows[i];

				// Check if it's a new group or not.
				bool isNewGroup = false;
				foreach (string order in OrderBy)
					if (Compare(currentRow[order], "!=", lastRow[order]))
						isNewGroup = true;

				// New group, so add results of aggregator into results table and reset count.
				if (isNewGroup)
				{
					// Set counts and averages.  
					foreach (DataColumn column in result.Columns)
					{
						if (column.ColumnName.Contains("count!"))
							aggregator[column] = groupCount;
						if (column.ColumnName.Contains("avg!"))
							aggregator[column] = ObjectDivide(aggregator[column], groupCount);
					}
					result.Rows.Add(aggregator);
					aggregator = result.NewRow();

					// Copy data from current Row into aggregator.  
					foreach (DataColumn column in result.Columns)
					{
						if (!column.ColumnName.Contains("count!"))
						{
							fullName = column.ColumnName.Contains('!') ? column.ColumnName.Split('!')[1] : column.ColumnName;
							aggregator[column] = currentRow[fullName];
						}
					}

					groupCount = 1;
				}

				// CurrentRow belongs to same group as last row.
				else
				{
					foreach (DataColumn column in result.Columns)
					{
						fullName = column.ColumnName.Contains('!') ? column.ColumnName.Split('!')[1] : column.ColumnName;
						if (column.ColumnName.Contains("sum!") || column.ColumnName.Contains("avg!"))
						{
							aggregator[column] = ObjectSum(aggregator[column], currentRow[fullName]);
						}
						if (column.ColumnName.Contains("max!"))
						{
							if (Compare(currentRow[fullName], ">", aggregator[column]))
								aggregator[column] = currentRow[fullName];
						}
						if (column.ColumnName.Contains("min!"))
						{
							if (Compare(currentRow[fullName], "<", aggregator[column]))
								aggregator[column] = currentRow[fullName];
						}

					}
					++groupCount;
				}
			}

			// Set counts and averages for last group.    
			foreach (DataColumn column in result.Columns)
			{
				if (column.ColumnName.Contains("count!"))
					aggregator[column] = groupCount;
				if (column.ColumnName.Contains("avg!"))
					aggregator[column] = ObjectDivide(aggregator[column], groupCount);
			}
			result.Rows.Add(aggregator);

			return result;
		}


		
		/// <summary>
		/// Select only the columns in Select list. Select statements comes in in its crude original form 
		/// (where aliases are kept and FullyQualified names haven't been set yet.)  
		/// This is to ensure that we preserve the user's desired renaming schemes.
		/// </summary>
		public static DataTable SelectTable(DataTable table, List<string> Select, List<string> From)
		{
			string columnName;
			string alias;
			string aggregate;

			for (int i=0; i<Select.Count; ++i)
			{
				aggregate = Select[i].Contains("!") ? Select[i].Split('!')[0].ToLower() : string.Empty;
				columnName = Select[i].Contains("!") ? aggregate + "!" + FullyQualifiedName(Select[i].Split('!', ':')[1], From) : FullyQualifiedName(Select[i].Split(':')[0], From);
				alias = Select[i].Contains(":") ? Select[i].Split(':')[1] : string.Empty;
				
				if (alias != string.Empty)
					Select[i] = alias; // Set to alias name.
				else if (Select[i].Contains('.'))
					Select[i] = columnName; // Set to real Table.Column to match casing.
				else
					Select[i] = aggregate != string.Empty ? aggregate + "!" + columnName.Split('.')[1] : columnName.Split('.')[1]; // Set to real ColumnName to match casing.

				table.Columns[columnName].ColumnName = Select[i];
			}

			for (int i=0; i<Select.Count; ++i)
				table.Columns[Select[i]].SetOrdinal(i);
			while (table.Columns.Count - Select.Count > 0)
				table.Columns.RemoveAt(table.Columns.Count-1);
				
			return table;
		}



	
		public static void SetTable(DataTable table, string Set)
		{
			if (Set != string.Empty)
			{
				if (DataSet.Tables.Contains(Set))
					throw new InvalidQueryException("Cannot set to an already existing table:  " + Set);
				foreach (DataColumn column in table.Columns)
					if (column.ColumnName.Contains('.'))
						throw new InvalidQueryException("Columns to be temporarily stored (from Set) cannot contain '.':  " + column.ColumnName + ".  Rename the column using ':'.");
				table.TableName = Set;
				DataSet.Tables.Add(table);
				DataSet.AcceptChanges();

				Log.Add(table.TableName, "New temporarily table (" + table.Rows.Count + " rows)");
			}
		}

		#endregion

		#region Delete

		public static void DeleteTable(string tableName)
		{
			if (!DataSet.Tables.Contains(tableName))
				throw new InvalidQueryException("Cannot delete non-existent table:  " + tableName);
			tableName = DataSet.Tables[tableName].TableName;

			// Remove from DataSet
			DataSet.Tables.Remove(tableName);
			DataSet.AcceptChanges();

			// Delete table.xml
			File.Delete("Data/" + tableName + ".xml");

			// Update schema.xml
			XmlDocument xmlSchema = new XmlDocument();
			xmlSchema.Load("Data/Schema.xml");
			foreach (XmlElement xTable in xmlSchema.DocumentElement.ChildNodes)
			{
				if (xTable.Name.ToLower() == tableName.ToLower())
				{
					xmlSchema.DocumentElement.RemoveChild(xTable);
					break;
				}
			}
			xmlSchema.Save("Data/Schema.xml");

			Log.Add(tableName, "Deleted");
		}

		/// <summary>
		/// Delete rows from the given table based on criteria specified in Where clause
		/// </summary>
		public static void DeleteRows(string tableName, List<List<Expression>> Where)
		{
			if (!DataSet.Tables.Contains(tableName))
				throw new InvalidQueryException("Cannot delete rows from a nonexistent table: " + tableName);
			tableName = DataSet.Tables[tableName].TableName; 

			// Delete rows
			DataTable table = DataSet.Tables[tableName];
			int preCount = table.Rows.Count;
			if (Where.Count > 0)
			{
				FilterTable(table, Where, true);
				//DataSet.Tables.Remove(tableName);
				//DataSet.Tables.Add(table);
			}
			else
				table = table.Clone(); // Delete all the rows.
			DataSet.AcceptChanges();

			int deleteCount = preCount - table.Rows.Count;

			// Update table.xml
			string tablePath = "Data/" + tableName + ".xml";
			File.Delete(tablePath);
			XmlDocument xTable = new XmlDocument();
			xTable.LoadXml("<" + tableName + " />");
			XmlElement root = xTable.DocumentElement;

			foreach (DataRow row in table.Rows)
			{
				XmlElement xRow = xTable.CreateElement("Row");
				foreach (DataColumn column in table.Columns)
				{
					XmlNode xCol = xTable.CreateElement(column.ColumnName);
					xCol.InnerText = row[column].ToString();
					xRow.AppendChild(xCol);
				}
				root.AppendChild(xRow);
			}
			xTable.Save(tablePath);

			Log.Add(tableName, "Deleted " + deleteCount + " rows");
		}

		#endregion

		#region Import

		/// <summary>
		/// At the provided xml file location, the method creates a new table, recursively runs through the file, and harvests data by adding data to rows and resulting rows to the table.
		/// </summary>
		public static void ImportTable(string xmlPath, string header, List<string> children)
		{
			// Harvest Data from xml
			DataTable table = new DataTable(header.Split(':')[0]);
			foreach (string child in children)
				table.Columns.Add(CreateColumn(child.Split(':')[0], "string"));

			XmlDocument xml = new XmlDocument();
			try { xml.Load(xmlPath); }
			catch { throw new InvalidQueryException("Invalid xml path:  " + xmlPath);}

			DataRow newRow = null;
			HarvestData(ref table, ref newRow, xml.DocumentElement); 


			// Build query to create a new table in database and insert values.
			string queryCreate = "create [" + (header.Contains(':') ? header.Split(':')[1] : header) + "]";
			foreach (string child in children)
			{
				queryCreate += " string [" + (child.Contains(':') ? child.Split(':')[1] : child) + "]";
			}
			string queryInsert = "insert " + "[" + (header.Contains(':') ? header.Split(':')[1] : header) + "]";

			foreach (DataRow row in table.Rows)
			{
				queryInsert += "\r\n values ";
				foreach (DataColumn column in table.Columns)
				{
					queryInsert += "[" + row[column].ToString() + "] ";
				}
			}

			// Run query.

			new Query(queryCreate).DoQuery();
			new Query(queryInsert).DoQuery();
		}

		private static void HarvestData(ref DataTable table, ref DataRow row, XmlNode node)
		{
			// New header reached. Add previous row to table and create new row.  
			if (table.TableName.ToLower() == node.Name.ToLower())
			{
				if (row != null)
				{
					table.Rows.Add(row);
					row = null;
				}
			}
			else if (table.Columns.Contains(node.Name))
			{
				if (row == null && node.InnerXml != string.Empty)
					row = table.NewRow();
				row[node.Name] = node.InnerXml;
			}

			foreach (XmlNode child in node.ChildNodes)
			{
				HarvestData(ref table, ref row, child);
			}
		}

		#endregion

		#region Helper Functions


		/// <summary>
		/// Create DataColumn with the default value set for the type if the type is indeed valid.  
		/// </summary>
		private static DataColumn CreateColumn(string name, string type)
		{
			try
			{
				switch (type.ToLower())
				{
					case "short": type = "Int16"; break;
					case "int": type = "Int32"; break;
					case "long": type = "Int64"; break;
					case "bool": type = "Boolean"; break;
					case "float": type = "Single"; break;
				}
				Type T = Type.GetType((type.ToLower().Contains("system") ? "" : "System.") + type, true, true);
				DataColumn column = new DataColumn(name, T);
				if (T.Name == "String")
					column.DefaultValue = String.Empty;
				else if (T.Name == "Char")
					column.DefaultValue = ' ';
				else if (T.IsValueType)
					column.DefaultValue = Activator.CreateInstance(T);
				else
					column.DefaultValue = null;
				return column;
			}
			catch { throw new InvalidQueryException("Unsupported type: " + type); }
		}

		/// <summary>
		/// Extension method - extends methods of DataColumn to return a fully qualified name:  TableName.ColumnName
		/// </summary>
		public static string ColumnFullName(this DataColumn column)
		{
			return column.Table.TableName + "." + column.ColumnName;
		}

		/// <summary>
		/// Extension method - extends method of DataColumnCollection to find if a table contains a fully qualified column name
		/// </summary>
		public static bool ContainsFullName(this DataColumnCollection columns, string compare)
		{
			foreach (DataColumn column in columns)
			{
				if (column.ColumnFullName().ToLower() == compare.ToLower())
					return true;
			}
			return false;
		}

		/// <summary>
		/// Searches database to create fully qualified column name:  TableName.ColumnName
		/// </summary>
		public static string FullyQualifiedName(string columnName, List<string> allowedTables)
		{

			foreach (string table in allowedTables)
				if (!DataHandler.DataSet.Tables.Contains(table))
					throw new InvalidQueryException("The table doesn't exist:" + table);

			List<string> fullName = new List<string>();
			if (columnName.Contains("."))
			{
				string table = columnName.Split('.')[0];
				string column = columnName.Split('.')[1];
				if (!allowedTables.Exists(s => s.ToLower() == table.ToLower()))
					throw new InvalidQueryException("The table is not allowed:" + columnName);
				else if (!DataHandler.DataSet.Tables[table].Columns.Contains(column))
					throw new InvalidQueryException("The column doesn't exist: " + columnName);
				fullName.Add(DataSet.Tables[table].Columns[column].ColumnFullName()); // Use original table/column to ensure correct casing
			}
			else // need to adjust to fully qualified name.
				foreach (string table in allowedTables)
					if (DataHandler.DataSet.Tables[table].Columns.Contains(columnName))
						fullName.Add(DataSet.Tables[table].Columns[columnName].ColumnFullName()); // Use original table/column to ensure correct casing

			if (fullName.Count == 0)
				throw new InvalidQueryException("The column does not exist in any table: " + columnName);
			if (fullName.Count > 1)
				throw new InvalidQueryException("The column name '" + columnName + "' is ambiguous: \r\n" + "Candidates: " + string.Join(" ", fullName.ToArray()));
			return fullName[0];
		}

		private static Boolean Compare(object x, string op, object y)
		{
			Type T = x.GetType();
			bool match = false;
			op = op.ToLower(); // Facilitates comparisons to string operators (ie. startswith, contains).

			if (!SyntaxProvider.Operators.Contains(op))
				throw new InvalidQueryException("Invalid operator " + op);
			if (SyntaxProvider.StringOperators.Contains(op) && T != typeof(string))
				throw new InvalidQueryException("Can only apply " + op + " on strings:  " + x.ToString());

			if (T == typeof(short) || T == typeof(int) || T == typeof(long) || T == typeof(float) || T == typeof(double) || T == typeof(decimal))
				T = typeof(decimal);

			try
			{
				x = Convert.ChangeType(x, T);
				y = Convert.ChangeType(y, T);
			}
			catch (Exception e) { throw new InvalidQueryException("Cannot cast " + x.ToString() + " or " + y.ToString() + " to " + T.Name); }

			try
			{
				if (T == typeof(string))
				{
					string xStr = (x as string).ToLower();
					string yStr = (y as string).ToLower();
					switch (op)
					{
						case "startswith": if (xStr.StartsWith(yStr)) match = true; break;
						case "endswith": if (xStr.EndsWith(yStr)) match = true; break;
						case "contains": if (xStr.Contains(yStr)) match = true; break;
						case "!startswith": if (!xStr.StartsWith(yStr)) match = true; break;
						case "!endswith": if (!xStr.EndsWith(yStr)) match = true; break;
						case "!contains": if (!xStr.Contains(yStr)) match = true; break;
					}
				}

				IComparable xCompare = (IComparable)x;
				IComparable yCompare = (IComparable)y;

				switch (op)
				{
					case "<": if (xCompare.CompareTo(yCompare) < 0) match = true; break;
					case "<=": if (xCompare.CompareTo(yCompare) <= 0) match = true; break;
					case "=": if (xCompare.CompareTo(yCompare) == 0) match = true; break;
					case "!=": if (xCompare.CompareTo(yCompare) != 0) match = true; break;
					case ">=": if (xCompare.CompareTo(yCompare) >= 0) match = true; break;
					case ">": if (xCompare.CompareTo(yCompare) > 0) match = true; break;
				}
			}
			catch (Exception e)
			{
				throw new InvalidQueryException("Cannot compare values:  " + x.ToString() + " " + op + " " + y.ToString());
			}
			
			return match;

		}

		private static object ObjectDivide(object val, int div)
		{
			Type T = val.GetType();
			if (T == typeof(short))
				return ((short)val) / div;
			if (T == typeof(int))
				return ((int)val) / div;
			if (T == typeof(long))
				return ((long)val) / div;
			if (T == typeof(double))
				return ((double)val) / div;
			if (T == typeof(decimal))
				return ((decimal)val) / div;
			
			throw new InvalidQueryException("Cannot find 'avg' for the column with type: " + val.GetType());
		}

		private static object ObjectSum(object val1, object val2)
		{
			Type T = val1.GetType();
			try
			{
				val2 = Convert.ChangeType(val2, T);

				if (T == typeof(short))
					return ((short)val1) + ((short)val2);
				if (T == typeof(int))
					return ((int)val1) + ((int)val2);
				if (T == typeof(long))
					return ((long)val1) + ((long)val2);
				if (T == typeof(double))
					return ((double)val1) + ((double)val2);
				if (T == typeof(decimal))
					return ((decimal)val1) + ((decimal)val2);
				if (T == typeof(string))
				{
					return ((string)val1) + " " + ((string)val2);
				}
				if (T == typeof(TimeSpan))
				{
					return ((TimeSpan)val1) + ((TimeSpan)val2);
				}
				if (T == typeof(bool))
				{
					return ((bool)val1) || ((bool)val2);
				}
				throw new Exception();
			}
			catch { throw new InvalidQueryException("Cannot find 'sum' for the column with type:  " + val1.GetType()); }
		} 

		#endregion


	}
}
