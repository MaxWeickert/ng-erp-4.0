﻿using Master40.DB.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Master40.DB.Data.Context
{
    public class MasterDbHelper
    {
        public async static Task<Article> GetArticleBomRecursive(MasterDBContext context, Article article, int ArticleId)
        {
            article.ArticleChilds = context.ArticleBoms.Include(a => a.ArticleChild)
                                                        .ThenInclude(w => w.WorkSchedules)
                                                        .Where(a => a.ArticleParentId == ArticleId).ToList();

            foreach (var item in article.ArticleChilds)
            {
                await GetArticleBomRecursive(context, item.ArticleParent, item.ArticleChildId);
            }
            await Task.Yield();
            return article;

        }


        public async static Task<ProductionOrder> GetProductionOrderBomRecursive(MasterDBContext context, ProductionOrder prodOrder, int productionOrderId)
        {
            prodOrder.ProdProductionOrderBomChilds = context.ProductionOrderBoms
                                                            .Include(a => a.ProductionOrderChild)
                                                            .ThenInclude(w => w.ProductionOrderWorkSchedule)
                                                            .Where(a => a.ProductionOrderParentId == productionOrderId).ToList();

            foreach (var item in prodOrder.ProdProductionOrderBomChilds)
            {
                await GetProductionOrderBomRecursive(context, item.ProductionOrderParent, item.ProductionOrderChildId);
            }
            await Task.Yield();
            return prodOrder;

        }

        public static Task<List<ProductionOrderWorkSchedule>> GetPriorProductionOrderWorkSchedules(MasterDBContext context, ProductionOrderWorkSchedule productionOrderWorkSchedule)
        {
            var rs = Task.Run(() =>
            {
                var priorItems = new List<ProductionOrderWorkSchedule>();
                // If == min Hirachie --> get Pevious Article -> Higest Hirachie Workschedule Item
                var maxHirachie = context.ProductionOrderWorkSchedule.Where(x => x.ProductionOrderId == productionOrderWorkSchedule.ProductionOrderId)
                                                    .Max(x => x.HierarchyNumber);

                if (maxHirachie == productionOrderWorkSchedule.HierarchyNumber)
                {
                    // get Previous Article
                    var priorBom = context.ProductionOrderBoms
                                                .Include(x => x.ProductionOrderParent)
                                                    .ThenInclude(x => x.ProductionOrderWorkSchedule)
                                                .Where(x => x.ProductionOrderChildId == productionOrderWorkSchedule.ProductionOrderId).ToList();

                    // out of each Part get Highest Workschedule
                    foreach (var item in priorBom)
                    {
                        var prior = item.ProductionOrderParent.ProductionOrderWorkSchedule.OrderBy(x => x.HierarchyNumber).FirstOrDefault();
                        if (prior != null)
                        {
                            priorItems.Add(prior);
                        }
                    }
                }
                else
                {
                    // get Previous Workschedule
                    priorItems.Add(context.ProductionOrderWorkSchedule.Where(x => x.ProductionOrderId == productionOrderWorkSchedule.ProductionOrderId
                                                            && x.HierarchyNumber > productionOrderWorkSchedule.HierarchyNumber)
                                                             .OrderByDescending(x => x.HierarchyNumber).FirstOrDefault());
                }
                return priorItems;
            });

            return rs;
        }





        /// <summary>
        /// copies am Article and his Childs to ProductionOrder
        /// Creates Demand Provider for Production oder and DemandRequests for childs
        /// </summary>
        /// <returns></returns>
        public static ProductionOrder CopyArticleToProductionOrder(MasterDBContext context, int articleId, decimal quantity, int demandRequesterId)
        {
            var article = context.Articles.Include(a => a.ArticleBoms).ThenInclude(c => c.ArticleChild).Single(a => a.Id == articleId);
            var mainProductionOrder = new ProductionOrder
            {
                ArticleId = article.Id,
                Name = "Prod. Auftrag: " + article.Name,
                Quantity = quantity,
            };
            context.ProductionOrders.Add(mainProductionOrder);

            var demandProvider = new DemandProviderProductionOrder()
            {
                ProductionOrderId = mainProductionOrder.Id,
                Quantity = quantity,
                ArticleId = article.Id,
                DemandRequesterId = demandRequesterId,
            };
            context.Demands.Add(demandProvider);

            foreach (var item in article.ArticleBoms)
            {
                var prodOrder = new ProductionOrder
                {
                    ArticleId = item.ArticleChildId,
                    Name = "Prod. Auftrag: " + article.Name,
                    Quantity = quantity * item.Quantity,
                };
                context.ProductionOrders.Add(prodOrder);



                var prodOrderBom = new ProductionOrderBom
                {
                    Quantity = quantity * item.Quantity,
                    ProductionOrderParentId = mainProductionOrder.Id,
                    ProductionOrderChildId = prodOrder.Id
                };
                context.ProductionOrderBoms.Add(prodOrderBom);

                var demandRequester = new DemandProductionOrderBom
                {
                    ProductionOrderBomId = prodOrderBom.Id,
                    Quantity = quantity,
                    ArticleId = item.ArticleChildId,
                    DemandRequesterId = demandProvider.Id, // nicht sicher
                };
                context.Demands.Add(demandRequester);

            }
            context.SaveChanges();

            return mainProductionOrder;
        }
    }
}