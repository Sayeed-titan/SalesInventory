using System;
using System . Collections . Generic;
using System . ComponentModel . DataAnnotations;
using System . ComponentModel . DataAnnotations . Schema;
using System . Linq;
using System . Web;

namespace SalesInventoryV1 . Models
{
      [Table ( "SalesOrders" )]
      public class SalesOrder
      {
            [Key]
            public int OrderId { get; set; }

            [Required]
            [StringLength ( 50 )]
            public string OrderNumber { get; set; }

            public int CustomerId { get; set; }
            public DateTime Orderdate { get; set; }

            [Column ( TypeName = "decimal(18, 2)" )]
            public decimal TotalAmount { get; set; }

            [StringLength ( 50 )]
            public string Status { get; set; }

            public DateTime CreatedDate { get; set; }

            public virtual Customer Customer { get; set; }

      }
}