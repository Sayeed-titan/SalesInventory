using System;
using System . Collections . Generic;
using System . ComponentModel . DataAnnotations;
using System . ComponentModel . DataAnnotations . Schema;
using System . Linq;
using System . Web;

namespace SalesInventoryV1 . Models
{
      [Table("Products")]
      public class Product
      {
            [Key]
            public int ProductId { get; set; }

            [Required]
            [StringLength(200)]
            public string ProductName { get; set; }   

            public int CategoryId { get; set; }

            [Column(TypeName = "decimal(18, 2)")]
            public decimal UnitPrice { get; set; }

            public int StockQuantity { get; set; }
            public int ReorderLevel { get; set; }
            public bool IsActive { get; set; } 
            public DateTime CreatedDate { get; set; }

            [ForeignKey("CategoryId")]
            public virtual Category Category { get; set; }
      }
}