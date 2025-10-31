using System;
using System . Collections . Generic;
using System . ComponentModel . DataAnnotations;
using System . ComponentModel . DataAnnotations . Schema;
using System . Linq;
using System . Web;

namespace SalesInventoryV1 . Models
{
      [Table ( "SalesOrderDetails" )]
      public class SalesOrderDetail
      {
            [Key]
            public int OrderDetailId { get; set; }

            public int OrderId { get; set; }
            public int ProductId { get; set; }
            public int Quantity { get; set; }

            [Column ( TypeName = "decimal(18, 2)" )]
            public decimal UnitPrice { get; set; }

            [Column ( TypeName = "decimal(18, 2)" )]
            public decimal SubTotal { get; set; }

            [ForeignKey("OrderId")] 
            public virtual SalesOrder SalesOrder { get; set; }

            [ForeignKey("ProductId")]
            public virtual Product Product { get; set; }
      }
}