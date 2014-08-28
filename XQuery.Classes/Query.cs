using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Data;

namespace XQuery.Classes
{

	public class Query
	{
		public QueryType Type;
		public string QueryString = string.Empty;
		public List<string> Create = new List<string>();
		public List<string> Insert = new List<string>();
		public List<List<string>> Values = new List<List<string>>();
		public List<string> Select = new List<string>();
		public List<string> From = new List<string>();
		public List<Expression> On = new List<Expression>();
		public List<List<Expression>> Where = new List<List<Expression>>();
		public string Set = string.Empty;
		public string Delete = string.Empty;
		public string Drop = string.Empty;
		public List<string> OrderBy = new List<string>();
		public List<string> Import = new List<string>();

		#region Constructor

		public Query(string queryString)
		{
			PopulateQuery(queryString);
		}

		private void PopulateQuery(string queryString)
		{
			QueryString = queryString;
			string command = String.Empty;
			string on = String.Empty;
			string where = String.Empty;
			List<string> delete = new List<string>();
			List<string> drop = new List<string>();
			List<string> set = new List<string>();

			List<string> words = ReformStrings(queryString.Split(SyntaxProvider.Separators.ToArray(), StringSplitOptions.RemoveEmptyEntries).ToList());

			foreach (string word in words)
			{

				// Select the appropriate command, continue, and the subsequent words will get added into the corresponding properties.
				switch (word.ToLower())
				{
					case "create": command = "create"; continue;
					case "insert": command = "insert"; continue;
					case "values": command = "values"; Values.Add(new List<string>()); continue;
					case "select": command = "select"; continue;
					case "from": command = "from"; continue;
					case "on": command = "on"; continue;
					case "where": command = "where"; continue;
					case "orderby": command = "orderby"; continue;
					case "delete": command = "delete"; continue;
					case "drop": command = "drop"; continue;
					case "set": command = "set"; continue;
					case "import": command = "import"; continue;
				}
				if (command == String.Empty)
					throw new InvalidQueryException("Unsupported query argument:  " + word);

				switch (command)
				{
					case "create": Create.Add(word); break;
					case "insert": Insert.Add(word); break;
					case "values": Values[Values.Count - 1].Add(word); break;
					case "select": Select.Add(word); break;
					case "from": From.Add(word); break;
					case "on":  if (word.ToLower() == "or") 
								   throw new InvalidQueryException("'Or' keyword not supported in ON statement"); 
								if (word.ToLower() == "and")
									on += word.ToLower() + " ";
								else 
									on += word + " "; break;
					case "where": if (word.ToLower() == "and" || word.ToLower() == "or")
									where += word.ToLower() + " ";
								  else
									where += word + " "; break;
					case "orderby": OrderBy.Add(word); break;
					case "delete":  delete.Add(word); break;
					case "drop":  drop.Add(word); break;
					case "set": set.Add(word); break;
					case "import": Import.Add(word); break;
				}
			}

			// Parse On and Where strings.
			foreach (string exp in on.Split(new string[] {" and "}, StringSplitOptions.RemoveEmptyEntries).ToList())
				On.Add(new Expression(exp));

			foreach (string andStr in where.Split(new string[] { " or " }, StringSplitOptions.RemoveEmptyEntries).ToList())
			{
				List<Expression> expressions = new List<Expression>();
				foreach (string exp in andStr.Split(new string[] { " and " }, StringSplitOptions.RemoveEmptyEntries).ToList())
					expressions.Add(new Expression(exp));
				Where.Add(expressions);
			}

			ReplaceAliases();

			if (delete.Count > 1 || drop.Count > 1)
				throw new InvalidQueryException("Can only delete or drop from one table");
			if (set.Count > 1)
				throw new InvalidQueryException("Can only set as one new table");
			if (set.Count == 1)
				Set = set[0];
			if (delete.Count == 1)
				Delete = delete[0]; 
			if (drop.Count == 1)
				Drop = drop[0];

			// Error Checking
			if (Select.Count > 0 && From.Count > 0)
				Type = QueryType.Select;
			else if (Create.Count > 0)
				Type = QueryType.Create;
			else if (Insert.Count > 0 && Values.Count > 0)
				Type = QueryType.Insert;
			else if (Delete != string.Empty)
				Type = QueryType.Delete;
			else if (Drop != string.Empty)
				Type = QueryType.Drop;
			else if (Import.Count > 0)
				Type = QueryType.Import;
			else
				throw new InvalidQueryException("Malformed query.  Check for missing clauses.");

		}



		#endregion

		#region Helper Methods

		/// <summary>
		/// Find any aliases the user set for a table in From, and replace the aliases in On, Where, and OrderBy (Preserve Select)
		/// </summary>
		private void ReplaceAliases()
		{
			for (int i = 0; i < From.Count; ++i)
			{
				if (From[i].Contains(':'))
				{
					string realName = From[i].Split(':')[0];
					string alias = From[i].Split(':')[1];
					From[i] = realName;
					foreach (Expression exp in On)
					{
						if (exp.Left.Contains("."))
							if (exp.Left.Split('.')[0].ToLower() == alias.ToLower())
								exp.Left = realName + "." + exp.Left.Split('.')[1];
						if (exp.Right.Contains("."))
							if (exp.Right.Split('.')[0].ToLower() == alias.ToLower())
								exp.Right = realName + "." + exp.Right.Split('.')[1];
					}
					foreach (List<Expression> and in Where)
						foreach (Expression exp in and)
						{
							if (exp.Left.Contains("."))
								if (exp.Left.Split('.')[0].ToLower() == alias.ToLower())
									exp.Left = realName + "." + exp.Left.Split('.')[1];
						}
					for (int j = 0; j < OrderBy.Count; ++j)
						if (OrderBy[j].Contains("."))
							if (OrderBy[j].Split('.')[0].ToLower() == alias.ToLower())
								OrderBy[j] = alias;
				}
			}
		}

		/// <summary>
		/// Replace all column names with their fully qualified name: TableName.ColumnName (Preserve Select)
		/// </summary>
		private void SetFullyQualifiedNames()
		{
			// From
			for (int i = 0; i < From.Count; ++i)
				if (!DataHandler.DataSet.Tables.Contains(From[i]))
					throw new InvalidQueryException("(From) The table doesn't exist: " + From[i]);

			// Select
			if (Select.Contains("*"))
			{
				Select.Clear();
				foreach (string tableName in From)
					foreach (DataColumn column in DataHandler.DataSet.Tables[tableName].Columns)
						Select.Add(tableName + "." + column.ColumnName);
			}

			// On
			for (int i = 0; i < On.Count; ++i)
			{
				On[i].Left = DataHandler.FullyQualifiedName(On[i].Left, From);
				On[i].Right = DataHandler.FullyQualifiedName(On[i].Right, From);
			}

			// Where
			for (int i = 0; i < Where.Count; ++i)
				for (int j = 0; j < Where[i].Count; ++j)
				{
					Where[i][j].Left = DataHandler.FullyQualifiedName(Where[i][j].Left, From);
				}


			// OrderBy
			for (int i = 0; i < OrderBy.Count; ++i)
			{
				if (OrderBy[i].ToLower() != "desc" && OrderBy[i].ToLower() != "descending" && OrderBy[i].ToLower() != "asc" && OrderBy[i].ToLower() != "ascending")
					OrderBy[i] = DataHandler.FullyQualifiedName(OrderBy[i], From);
			}
		}


		/// <summary>
		/// Takes a list of single words and checks for any [ ] to reform collection of words back into one string.
		/// </summary>
		public static List<string> ReformStrings(List<string> words)
		{
			List<string> reformed = new List<string>();
			bool isAppending = false;
			string appendStr = string.Empty;

			foreach (string word in words)
			{
				if (word.Contains("[") && word.Contains("]") && !isAppending)
					reformed.Add(word.Replace("[", "").Replace("]", ""));
				else if (word.Contains("[") && !word.EndsWith("]"))
				{
					isAppending = true;
					appendStr += word + " ";
				}
				else if (word.Contains("]"))
				{
					appendStr += word;
					appendStr = appendStr.Replace("[", "").Replace("]", "").Trim();
					appendStr = string.Join(".", appendStr.Split(new char[]{'.'}, StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim()).ToArray());
					appendStr = string.Join(":", appendStr.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim()).ToArray());
					reformed.Add(appendStr);
					appendStr = String.Empty;
					isAppending = false;
				}
				else if (isAppending)
					appendStr += word + " ";
				else
					reformed.Add(word.Replace("[", "").Replace("]", ""));
			}

			if (isAppending)
			    throw new InvalidQueryException("All '[' should have a matching ']':  " + appendStr);

			return reformed;
		}

		private void ParseOrderBy(ref List<string> sort, ref List<string> direction)
		{
			List<string> validDirections = new List<string>() { "desc", "descending", "asc", "ascending" };
			int i = 0;
			while (i < OrderBy.Count)
			{
				if (validDirections.Exists(s => s == OrderBy[i].ToLower()))
					throw new InvalidQueryException("Invalid OrderBy expression.  Be sure to place direction arguments (desc, descending, asc, ascending) after the column name.");

				sort.Add(OrderBy[i]);
				++i;
				if (i >= OrderBy.Count)
					direction.Add("asc");
				else if (validDirections.Exists(s => s == OrderBy[i].ToLower()))
				{
					direction.Add(OrderBy[i].ToLower());
					++i;
				}
				else
					direction.Add("asc");
			}
		}
		#endregion

		#region Factory Methods

		/// <summary>
		/// Parses a query string and returns a list of all query blocks. 
		/// </summary>		
		public static List<Query> Parse(string queryString)
		{
			List<Query> queries = new List<Query>();
			List<string> keyWords = new List<string>(){"select", "insert", "delete", "drop", "create", "import"};
			List<string> words = new List<string>();

			
			words = queryString.Split(SyntaxProvider.Separators.ToArray(), StringSplitOptions.RemoveEmptyEntries).ToList();

			string block = string.Empty;
			int i = 0;
			while (i < words.Count)
			{
				if (keyWords.Contains(words[i].ToLower()))
				{
					block = string.Empty;
					block += words[i] + " ";
					++i;
					while (i < words.Count && !keyWords.Contains(words[i].ToLower()))
					{
						block += words[i] + " ";
						++i;
					}
					queries.Add(new Query(block));
				}
			}
			return queries;
		}
		#endregion

		#region DoQuery
		/// <summary>
		/// Performs the appropriate query and returns a datatable.  
		/// </summary>
		/// <returns></returns>
		public DataTable DoQuery()
		{
			switch (Type)
			{
				case QueryType.Create:  DoCreate(); return null;
				case QueryType.Insert:  DoInsert(); return null;
				case QueryType.Select:  return DoSelect();
				case QueryType.Delete:  DoDelete(); return null;
				case QueryType.Drop:    DoDrop(); return null;
				case QueryType.Import:  DoImport(); return null;
				default: return null;
			}
		}

		private void DoCreate()
		{
			string tableName = Create[0];
			List<string> columns = new List<string>();
			List<string> types = new List<string>();
			for (int i=1; i<Create.Count(); ++i)
				if (i%2 == 1)
					types.Add(Create[i]);
				else
					columns.Add(Create[i]);

			DataHandler.CreateTable(tableName, columns, types);
		}

		private void DoInsert()
		{
			string tableName = Insert[0];
			List<string> columns = new List<string>();
			for (int i=1; i<Insert.Count(); ++i)
				columns.Add(Insert[i]);

			DataHandler.InsertValues(tableName, columns, Values);
		}

		private DataTable DoSelect()
		{
			// Update On, Where, OrderBy column names to fully qualified name TableName.ColumnName
			SetFullyQualifiedNames();

			List<string> sort = new List<string>();
			List<string> direction = new List<string>();
			ParseOrderBy(ref sort, ref direction);

			DataTable result; 
			result = DataHandler.JoinTables(From, On);
			result = DataHandler.FilterTable(result, Where);
			result = DataHandler.SortTable(result, sort, direction);
			result = DataHandler.GroupTable(result, OrderBy, Select, From);
			result = DataHandler.SelectTable(result, Select, From);
			DataHandler.SetTable(result, Set);

			return result;
		}

		private void DoDelete()
		{
			DataHandler.DeleteRows(Delete, Where);
		}

		private void DoDrop()
		{
			DataHandler.DeleteTable(Drop);
		}

		private void DoImport()
		{
			if (Import.Count < 3)
				throw new InvalidQueryException("Invalid Import syntax. Requires 1 xml file path, 1 table name, 1 or more column names.");
			string xmlPath = Import[0];
			string tableName = Import[1];
			List<string> columns = new List<string>();
			for (int i = 2; i < Import.Count; ++i)
				columns.Add(Import[i]);

			DataHandler.ImportTable(xmlPath, tableName, columns);
		}

		#endregion

	}
}