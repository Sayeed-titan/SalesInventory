using System;
using System . Collections . Generic;
using System . ComponentModel . DataAnnotations;
using System . ComponentModel . DataAnnotations . Schema;
using System . Linq;
using System . Web;

namespace SalesInventoryV1 . Models
{
      [Table("Customers")]
      public class Customer
      {
            [Key]
            public int CustomerId { get; set; }

            [Required]
            [StringLength(200)]
            public string CustomerName { get; set; }

            [StringLength(100)]
            public string Email { get; set; }

            [StringLength(20)]
            public string Phone { get; set; }

            [StringLength(500)]
            public string Address { get; set; }

            [StringLength(100)]
            public string City { get; set; }

            [StringLength(100)]
            public string Country { get; set; }

            public DateTime CreatedDate { get; set; }


      }
}