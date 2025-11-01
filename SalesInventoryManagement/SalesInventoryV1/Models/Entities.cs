// Models/Entities.cs
// All entity models in one file for convenience

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesInventoryV1.Models
{
    // ===== Category Entity =====
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

        // Navigation property
        public virtual ICollection<Product> Products { get; set; }
    }

    // ===== Product Entity =====
    [Table("Products")]
    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        [Required]
        [StringLength(200)]
        public string ProductName { get; set; }

        public int CategoryId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        public int StockQuantity { get; set; }
        public int ReorderLevel { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }

        // Navigation property
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }
    }

    // ===== Customer Entity =====
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

        // Navigation property
        public virtual ICollection<SalesOrder> SalesOrders { get; set; }
    }

    // ===== SalesOrder Entity =====
    [Table("SalesOrders")]
    public class SalesOrder
    {
        [Key]
        public int OrderId { get; set; }

        [Required]
        [StringLength(50)]
        public string OrderNumber { get; set; }

        public int CustomerId { get; set; }
        public DateTime OrderDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [StringLength(50)]
        public string Status { get; set; }

        public DateTime CreatedDate { get; set; }

        // Navigation properties
        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }

        public virtual ICollection<SalesOrderDetail> OrderDetails { get; set; }
    }

    // ===== SalesOrderDetail Entity =====
    [Table("SalesOrderDetails")]
    public class SalesOrderDetail
    {
        [Key]
        public int OrderDetailId { get; set; }

        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        // Navigation properties
        [ForeignKey("OrderId")]
        public virtual SalesOrder SalesOrder { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
    }
}