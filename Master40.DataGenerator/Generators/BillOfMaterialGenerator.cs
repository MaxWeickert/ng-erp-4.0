using System.Collections.Generic;
using Master40.DataGenerator.DataModel.ProductStructure;
using Master40.DataGenerator.Util;
using Master40.DB.DataModel;

namespace Master40.DataGenerator.Generators
{
    public class BillOfMaterialGenerator
    {

        public void GenerateBillOfMaterial(List<Dictionary<long, Node>> nodesPerLevel, XRandom rng)
        {
            for (var k = 0; k < nodesPerLevel.Count - 1; k++)
            {
                foreach (var article in nodesPerLevel[k].Values)
                {
                    List<List<Edge>> incomingMaterialAllocation = new List<List<Edge>>();
                    foreach (var operation in article.Operations)
                    {
                        incomingMaterialAllocation.Add(new List<Edge>());
                    }

                    foreach (var edge in article.IncomingEdges)
                    {
                        var operationNumber = rng.Next(incomingMaterialAllocation.Count);
                        incomingMaterialAllocation[operationNumber].Add(edge);
                    }

                    List<List<Edge>> possibleSetsForFirstOperation =
                        incomingMaterialAllocation.FindAll(x => x.Count > 0);
                    var randomSet = rng.Next(possibleSetsForFirstOperation.Count);
                    List<Edge> firstOperation = possibleSetsForFirstOperation[randomSet];

                    List<List<Edge>> bom = new List<List<Edge>>();
                    incomingMaterialAllocation.Remove(firstOperation);
                    bom.Add(firstOperation);
                    bom.AddRange(incomingMaterialAllocation);

                    for (var i = 0; i < bom.Count; i++)
                    {
                        for (var j = 0; j < bom[i].Count; j++)
                        {
                            var name = "[" + bom[i][j].Start.Article.Name + "] in (" +
                                       article.Operations[i].MOperation.Name + ")";
                            var articleBom = new M_ArticleBom()
                            {
                                ArticleChildId = bom[i][j].Start.Article.Id,
                                Name = name, Quantity = (decimal) bom[i][j].Weight,
                                ArticleParentId = article.Article.Id,
                                OperationId = article.Operations[i].MOperation.Id
                            };
                            article.Operations[i].Bom.Add(articleBom);
                        }
                    }
                }
            }
        }

    }
}