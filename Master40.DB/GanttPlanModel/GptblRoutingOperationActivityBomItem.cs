﻿namespace Master40.DB.GanttPlanModel
{
    public partial class GptblRoutingOperationActivityBomItem
    {
        public string ClientId { get; set; }
        public string RoutingId { get; set; }
        public string OperationId { get; set; }
        public string AlternativeId { get; set; }
        public int ActivityId { get; set; }
        public int SplitId { get; set; }
        public string BomId { get; set; }
        public string BomItemId { get; set; }
    }
}
