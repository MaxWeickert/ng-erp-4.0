﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Master40.BusinessLogic.Helper;
using Master40.Data;
using Master40.Models;
using Master40.Models.DB;
using Microsoft.EntityFrameworkCore;

namespace Master40.BusinessLogic.MRP
{

    interface IScheduling
    {
        Task<ManufacturingSchedule> CreateSchedule(int orderId, List<ProductionOrder> productionOrders);
        Task<ManufacturingSchedule> BackwardScheduling(ManufacturingSchedule manufacturingSchedule );
        void ForwardScheduling(ManufacturingSchedule manufacturingSchedule);
        void CapacityScheduling();
    }

    class Scheduling : IScheduling
    {
        private readonly MasterDBContext _context;
        public List<LogMessage> Logger { get; set; }
        public Scheduling(MasterDBContext context)
        {
            Logger = new List<LogMessage>();
            _context = context;
        }

        public async Task<ManufacturingSchedule> CreateSchedule(int orderId, List<ProductionOrder> productionOrders)
        {

            var manufacturingSchedule = new ManufacturingSchedule();
            await Task.Run(() => {
            

                var orders = productionOrders;
                var headOrder = _context.Orders.Single(a => a.OrderId == orderId);

                var msg = "ProductionOrders received: " + orders.Count;
                Logger.Add(new LogMessage() { MessageType = MessageType.info, Message = msg });

                var workSchedules = new List<ProductionOrderWorkSchedule>();
                var po2Pows = _context.ProductionOrderToProductionOrderWorkSchedules;

                var timeHelper = headOrder.DueTime;

                foreach (var order in orders)
                {
                    //get abstract workSchedule
                    var abstractWorkSchedule = _context.WorkSchedules.Single(a => a.WorkScheduleId ==
                                                                              _context.WorkSchedules.Include(b => b.MachineGroup)
                                                                                  .Single(b => b.ArticleId == order.ArticleId)
                                                                                  .WorkScheduleId
                    );

                    //add specific workSchedule
                    workSchedules.Add(new ProductionOrderWorkSchedule()
                    {
                        //ToDo: duration to decimal?
                        Duration = abstractWorkSchedule.Duration * (int)order.Quantity,
                        HierarchyNumber = abstractWorkSchedule.HierarchyNumber,
                        MachineGroupId = abstractWorkSchedule.MachineGroupId,
                        MachineGroup = abstractWorkSchedule.MachineGroup,
                        MachineTool = abstractWorkSchedule.MachineTool,
                        MachineToolId = abstractWorkSchedule.MachineToolId,
                        Name = abstractWorkSchedule.Name,
                        ProductionOrderToWorkSchedules = null

                    });
                    //add connection between workSchedule and productionOrder
                    po2Pows.Add(new ProductionOrderToProductionOrderWorkSchedule()
                    {
                        ProductionOrder = order,
                        ProductionOrderId = order.ProductionOrderId,
                        ProductionOrderWorkSchedule = workSchedules.Last(),
                        ProductionOrderWorkScheduleId = workSchedules.Last().ProductionOrderWorkScheduleId

                    });
                    workSchedules.Last().ProductionOrderToWorkSchedules = new Collection<ProductionOrderToProductionOrderWorkSchedule>()
                    {
                       po2Pows.Last()
                    };
                    //find parents
                    var parents = _context.ArticleBoms.Include(a => a.ArticleParent).Where(c => c.ArticleChildId == order.ArticleId);
                    List<int> parentsId;
                    if (parents.Any())
                        parentsId = (from p in parents select new { p.ArticleParent.ArticleId }).Cast<int>().ToList();
                    else
                        parentsId = null;
                    //find children
                    var children = _context.ArticleBoms.Include(a => a.ArticleChild).Where(c => c.ArticleChildId == order.ArticleId);
                    List<int> childrenId;
                    if (children.Any())
                        childrenId = (from p in children select new { p.ArticleChild.ArticleId }).Cast<int>().ToList();
                    else
                        childrenId = null;
                    if (workSchedules.Last().MachineGroupId != null)
                    {
                        //add Item of manufacturingSchedule, which is used for backwardTermination etc.
                        manufacturingSchedule.items.Add(new ManufacturingScheduleItem()
                        {
                            //ToDo: write over to ProductionOrder -> new ProductionOrders from Forecast don´t have ID for WorkSchedules
                            MachineGroupId = workSchedules.Last().MachineGroup.MachineGroupId,
                            EndTime = -1,
                            StartTime = timeHelper,
                            ProductionOrderId = order.ProductionOrderId,
                            ArticleId = order.ArticleId,
                            Duration = workSchedules.Last().Duration,
                            ParentsArticleId = parentsId,
                            ChildrenArticleId = childrenId
                        });
                    }
                    else
                    {
                        //add Item of manufacturingSchedule, which is used for backwardTermination etc.
                        manufacturingSchedule.items.Add(new ManufacturingScheduleItem()
                        {
                            //ToDo: write over to ProductionOrder -> new ProductionOrders from Forecast don´t have ID for WorkSchedules
                            EndTime = -1,
                            StartTime = timeHelper,
                            ProductionOrderId = order.ProductionOrderId,
                            ArticleId = order.ArticleId,
                            Duration = workSchedules.Last().Duration,
                            ParentsArticleId = parentsId,
                            ChildrenArticleId = childrenId
                        });
                    }
                        
                }
                //ToDo: push to database po2Pows  and workSchedules and propably manufacturingSchedule
            });
            return manufacturingSchedule;
        }
        async Task<ManufacturingSchedule> IScheduling.BackwardScheduling(ManufacturingSchedule manufacturingSchedule)
        {
            await Task.Run(() => {
                manufacturingSchedule = Backward(manufacturingSchedule, 0);
                //ToDo: propably push Schedule
            });
            return manufacturingSchedule;
        }

        private ManufacturingSchedule Backward(ManufacturingSchedule manufacturingSchedule,
            int current)
        {
            //initialize start- and endtime to duetime
            var endTime = manufacturingSchedule.items.First().StartTime;
            manufacturingSchedule.items.ElementAt(current).EndTime = endTime;
            manufacturingSchedule.items.ElementAt(current).StartTime = endTime - manufacturingSchedule.items.ElementAt(current).Duration;
            
            //find parents and make sure to finish before they start
            for (int i = 0; i < manufacturingSchedule.items.ElementAt(current).ParentsArticleId.Count; i++)
            {
                for (int j = 0; j < manufacturingSchedule.items.Count; j++)
                {
                    if (manufacturingSchedule.items.ElementAt(j).ArticleId == manufacturingSchedule.items.ElementAt(current).ParentsArticleId.ElementAt(i))
                    {
                        if (manufacturingSchedule.items.ElementAt(current).EndTime > manufacturingSchedule.items.ElementAt(j).StartTime)
                            manufacturingSchedule.items.ElementAt(current).EndTime = manufacturingSchedule.items.ElementAt(j).StartTime;
                    }

                }
               
            }
            //find next free spot on the machine
            for (int i = 0; i < manufacturingSchedule.items.Count; i++)
            {
                if (manufacturingSchedule.items.ElementAt(i).MachineGroupId == manufacturingSchedule.items.ElementAt(current).MachineGroupId)
                {
                    if (manufacturingSchedule.items.ElementAt(i).StartTime > -1)
                        if ((manufacturingSchedule.items.ElementAt(i).StartTime < manufacturingSchedule.items.ElementAt(current).StartTime 
                                && manufacturingSchedule.items.ElementAt(i).EndTime > manufacturingSchedule.items.ElementAt(current).StartTime )
                            || manufacturingSchedule.items.ElementAt(i).StartTime < manufacturingSchedule.items.ElementAt(current).EndTime
                                && manufacturingSchedule.items.ElementAt(i).StartTime > manufacturingSchedule.items.ElementAt(current).StartTime)
                        { 
                            manufacturingSchedule.items.ElementAt(current).EndTime = manufacturingSchedule.items.ElementAt(i).StartTime;
                            manufacturingSchedule.items.ElementAt(current).StartTime =
                                manufacturingSchedule.items.ElementAt(current).EndTime -
                                manufacturingSchedule.items.ElementAt(current).Duration;
                            i = -1;
                        }
                }
            }

            //call children to be calculated
            foreach (var child in manufacturingSchedule.items.ElementAt(current).ChildrenArticleId)
            {
                for (int i=0; i < manufacturingSchedule.items.Count; i++)
                {
                    if (manufacturingSchedule.items.ElementAt(i).ArticleId == child)
                    {
                        Backward(manufacturingSchedule, i);
                        break;
                    }
                }
            }
            return manufacturingSchedule;
        }


        async void IScheduling.ForwardScheduling(ManufacturingSchedule manufacturingSchedule)
        {
            await Task.Run(() => {
                // Code you want to run Async
            });
        }

        async void IScheduling.CapacityScheduling()
        {
            await Task.Run(() => {
                // Code you want to run Async
            });
        }
        
    }
}
