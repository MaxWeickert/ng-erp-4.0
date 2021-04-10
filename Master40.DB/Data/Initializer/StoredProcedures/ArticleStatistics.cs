using Master40.DB.Data.Context;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Master40.DB.Data.Initializer.StoredProcedures
{
    public class ArticleStatistics
    {
        public static void CreateProcedures(MasterDBContext ctx)
        {

			string sql = string.Format(
			@"CREATE OR ALTER PROCEDURE ArticleCTE
				@ArticleId int
			AS
			BEGIN
				SET NOCOUNT ON;
				DROP TABLE IF EXISTS dbo.#Temp;
				DROP TABLE IF EXISTS dbo.#Union;
				WITH Parts(AssemblyID, ComponentID, PerAssemblyQty, ComponentLevel) AS  
				(  
					SELECT b.ArticleParentId, b.ArticleChildId, CAST(b.Quantity AS decimal),0 AS ComponentLevel  
					FROM dbo.M_ArticleBom  AS b  
					join dbo.M_Article a on a.Id = b.ArticleParentId
					where @ArticleId = a.Id
					UNION ALL  
					SELECT bom.ArticleParentId, bom.ArticleChildId, CAST(PerAssemblyQty * bom.Quantity as DECIMAL), ComponentLevel + 1  
					 FROM dbo.M_ArticleBom  AS bom  
						INNER join dbo.M_Article ac on ac.Id = bom.ArticleParentId
						INNER JOIN Parts AS p ON bom.ArticleParentId = p.ComponentID  
				)
				select * into #Temp 
				from (
					select pr.Id,pr.Name, Sum(p.PerAssemblyQty) as qty, pr.ToBuild as ToBuild
					FROM Parts AS p INNER JOIN M_Article AS pr ON p.ComponentID = pr.Id
					Group By pr.Id, pr.Name, p.ComponentID, pr.ToBuild) as x

				select * into #Union from (
					select Sum(o.Duration * t.qty) as dur, sum(t.qty) as count ,0 as 'Po'
						from dbo.M_Operation o join #Temp t on t.Id = o.ArticleId
						where o.ArticleId in (select t.Id from #Temp t)
				UNION ALL
					SELECT SUM(ot.Duration) as dur, COUNT(*) as count , 0 as 'Po'
						from dbo.M_Operation ot where ot.ArticleId = @ArticleId ) as x
				UNION ALL 
					SELECT 0 as dur, 0 as count, sum(t.qty) + 1 as 'Po'
					from #Temp t where t.ToBuild = 1
				select Sum(u.dur) as SumDuration , sum(u.count) as SumOperations, sum(u.Po)  as ProductionOrders from #Union u
			END");


			using (var command = ctx.Database.GetDbConnection().CreateCommand())
			{
				command.CommandText = sql;
				ctx.Database.OpenConnection();
				command.ExecuteNonQuery();
			}
		}
		//!!TODO: Build an object an return all values in one function.
		public static long DeliveryDateEstimator(int articleId, double factor, MasterDBContext dBContext)
        {
			var sql = string.Format("Execute ArticleCTE {0}", articleId);
			var estimatedProductDelivery = 2880L;
			using (var command = dBContext.Database.GetDbConnection().CreateCommand())
			{
				
				command.CommandText = sql;
				dBContext.Database.OpenConnection();
				using (var reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						System.Diagnostics.Debug.WriteLine(string.Format("Summe der Dauer {0}; Summe der Operationen {1}; Summe der Produktionsaufträge {2}", reader[0], reader[1], reader[2]));
						// TODO Catch false informations
						estimatedProductDelivery = (long)(System.Convert.ToInt64(reader[0]) * factor);
						System.Diagnostics.Debug.WriteLine("Estimated Product Delivery{0}", estimatedProductDelivery);
					}

				}
			}
			return estimatedProductDelivery;
		}

		public static long GetSumDuration(int articleId, MasterDBContext dBContext)
		{
			var sql = string.Format("Execute ArticleCTE {0}", articleId);
			var sumDuration = 0L;
			using (var command = dBContext.Database.GetDbConnection().CreateCommand())
			{
				command.CommandText = sql;
				dBContext.Database.OpenConnection();
				using (var reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						sumDuration = (long)System.Convert.ToInt64(reader[0]);
					}
				}
			}
			return sumDuration;
		}

		public static long GetSumOperations(int articleId, MasterDBContext dBContext)
		{
			var sql = string.Format("Execute ArticleCTE {0}", articleId);
			var sumOperations = 0L;
			using (var command = dBContext.Database.GetDbConnection().CreateCommand())
			{
				command.CommandText = sql;
				dBContext.Database.OpenConnection();
				using (var reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						sumOperations = (long)System.Convert.ToInt64(reader[1]);
					}
				}
			}
			return sumOperations;
		}

		public static long GetProductionOrders(int articleId, MasterDBContext dBContext)
		{
			var sql = string.Format("Execute ArticleCTE {0}", articleId);
			var productionOrders = 0L;
			using (var command = dBContext.Database.GetDbConnection().CreateCommand())
			{
				command.CommandText = sql;
				dBContext.Database.OpenConnection();
				using (var reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						productionOrders = (long)System.Convert.ToInt64(reader[2]);
					}
				}
			}
			return productionOrders;
		}

		/*public Dictionary<string, long> ProductCharacteristics(int articleId, MasterDBContext dBContext)
		{
			var sql = string.Format("Execute ArticleCTE {0}", articleId);
			//var estimatedProductDelivery = 2880L;
			//var productCharacteristics = new { sumDuration = "0", sumOperations = "0", productionOrders = "0" };
*//*			var productCharacteristics = new Dictionary<string, long> {
				{ "sumDuration", 0 },
				{ "sumOperations", 0 },
				{ "productionOrders", 0 }
			};*//*
			//var productCharacteristics = new Dictionary<string, long>{ sumDuration: "0", sumOperations: "0", productionOrders: "0" };
			using (var command = dBContext.Database.GetDbConnection().CreateCommand())
			{

				command.CommandText = sql;
				dBContext.Database.OpenConnection();
				using (var reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						System.Diagnostics.Debug.WriteLine(string.Format("Summe der Dauer {0}; Summe der Operationen {1}; Summe der Produktionsaufträge {2}", reader[0], reader[1], reader[2]));
						// TODO Catch false informations
						//var productCharacteristics = new { sumDuration: (long)System.Convert.ToInt64(reader[0]), sumOperations: (long)System.Convert.ToInt64(reader[1]), productionOrders: (long)System.Convert.ToInt64(reader[2])}
						//productCharacteristics = new { sumDuration = (long)System.Convert.ToInt64(reader[0]), sumOperations = (long)System.Convert.ToInt64(reader[1]), productionOrders = (long)System.Convert.ToInt64(reader[2]) };
						//productCharacteristics.sumDuration = (long)System.Convert.ToInt64(reader[0]);
						productCharacteristics.sumDuration = "1";
					}

				}
			}
			return productCharacteristics;
		}*/

	}
}
