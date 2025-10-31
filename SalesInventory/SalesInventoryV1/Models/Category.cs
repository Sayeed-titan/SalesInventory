using System;
using System . Collections . Generic;
using System . ComponentModel . DataAnnotations;
using System . ComponentModel . DataAnnotations . Schema;
using System . Linq;
using System . Web;

namespace SalesInventoryV1 . Models
{
      [Table("Categories")]
      public class Category
      {
            [Key]
            public int CategoryId { get; set; }

            [Required]
            [StringLength(100)]
            public string CategoryName { get; set; }

            [StringLength(500)]
            public string Description { get; set; }

            public DateTime CreatedDate { get; set; }

            public virtual ICollection<Product> Products { get; set; }
      }
}